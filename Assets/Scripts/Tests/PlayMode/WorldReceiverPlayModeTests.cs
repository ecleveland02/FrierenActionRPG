using System;
using System.Collections;
using Frieren.Core.Magic;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// The parts of the world receivers that are about time passing: draining, decaying, burning out.
    /// </summary>
    /// <remarks>
    /// Their decisions are covered by edit-mode tests, which is where a decision belongs. What could
    /// not be covered there is that a half-filled trough empties again if you wander off, and that a
    /// half-finished repair comes apart - the two behaviours that make those puzzles have a cost.
    /// </remarks>
    public sealed class WorldReceiverPlayModeTests : PlayModeTestBase
    {
        /// <summary>
        /// Builds the receiver inactive, configures it, then activates it.
        /// </summary>
        /// <remarks>
        /// <c>FlammableObject</c> reads its thresholds once in <c>Awake</c> to build a
        /// <c>BurnState</c>, so tuning set after the component is added would never reach it and the
        /// test would quietly measure the defaults instead.
        /// </remarks>
        private T Spawn<T>(string name, Action<T> configure = null) where T : Component
        {
            var host = new GameObject(name);
            host.SetActive(false);
            World.Track(host);

            T component = host.AddComponent<T>();
            configure?.Invoke(component);
            host.SetActive(true);
            return component;
        }

        private static bool Send(IMagicReceiver receiver, MagicElement element, float magnitude) =>
            receiver.ReceiveMagic(new MagicPulse(element, magnitude, Vector3.zero));

        [UnityTest]
        public IEnumerator AHalfFilledTroughDrainsAgain()
        {
            WaterBasin basin = Spawn<WaterBasin>("Trough", b =>
            {
                TestFields.Set(b, "fillThreshold", 20f);
                TestFields.Set(b, "drainPerSecond", 10f);
            });

            Send(basin, MagicElement.Water, 12f);
            Assert.AreEqual(BasinState.Empty, basin.State);
            Assert.Greater(basin.NormalizedFill, 0.5f);

            yield return Wait(1.0f);

            Assert.Less(basin.NormalizedFill, 0.2f,
                "Ten a second for a second should have taken most of it back.");
        }

        [UnityTest]
        public IEnumerator AFullTroughDoesNotDrain()
        {
            WaterBasin basin = Spawn<WaterBasin>("Trough", b =>
            {
                TestFields.Set(b, "fillThreshold", 20f);
                TestFields.Set(b, "drainPerSecond", 10f);
            });

            Send(basin, MagicElement.Water, 20f);
            Assert.AreEqual(BasinState.Filled, basin.State);

            yield return Wait(1.0f);

            Assert.AreEqual(BasinState.Filled, basin.State, "A filled basin holds what it has.");
        }

        [UnityTest]
        public IEnumerator FrozenSurvivesIndefinitely()
        {
            WaterBasin basin = Spawn<WaterBasin>("Trough");
            Send(basin, MagicElement.Water, 30f);
            Send(basin, MagicElement.Cold, 30f);

            Assert.AreEqual(BasinState.Frozen, basin.State);

            yield return Wait(1.2f);

            Assert.AreEqual(BasinState.Frozen, basin.State,
                "An ice bridge that melts on its own would be a trap, not a solution.");
        }

        [UnityTest]
        public IEnumerator AnUnfinishedRepairComesApart()
        {
            RepairableObject pillar = Spawn<RepairableObject>("Pillar", p =>
            {
                TestFields.Set(p, "repairThreshold", 30f);
                TestFields.Set(p, "decayPerSecond", 20f);
            });

            Send(pillar, MagicElement.Restoration, 20f);
            Assert.IsFalse(pillar.IsIntact);
            Assert.Greater(pillar.NormalizedProgress, 0.5f);

            yield return Wait(1.0f);

            Assert.Less(pillar.NormalizedProgress, 0.1f, "Letting go early must cost the progress.");
        }

        [UnityTest]
        public IEnumerator SustainedRestorationFinishesTheRepair()
        {
            RepairableObject pillar = Spawn<RepairableObject>("Pillar", p =>
            {
                TestFields.Set(p, "repairThreshold", 30f);
                TestFields.Set(p, "decayPerSecond", 3f);
            });

            // Four ticks of a channel, at the interval Mending actually uses.
            for (int i = 0; i < 10 && !pillar.IsIntact; i++)
            {
                Send(pillar, MagicElement.Restoration, 4f);
                yield return Wait(0.25f);
            }

            Assert.IsTrue(pillar.IsIntact, "Holding the spell should get there in about two seconds.");
        }

        [UnityTest]
        public IEnumerator AMendedPillarStaysMended()
        {
            RepairableObject pillar = Spawn<RepairableObject>("Pillar", p =>
            {
                TestFields.Set(p, "repairThreshold", 10f);
                TestFields.Set(p, "decayPerSecond", 50f);
            });

            Send(pillar, MagicElement.Restoration, 10f);
            Assert.IsTrue(pillar.IsIntact);

            yield return Wait(0.6f);

            Assert.IsTrue(pillar.IsIntact, "Decay must not run once the thing is whole.");
        }

        [UnityTest]
        public IEnumerator AFlammableBurnsOutAndDisables()
        {
            FlammableObject crate = Spawn<FlammableObject>("Crate", c =>
            {
                TestFields.Set(c, "ignitionThreshold", 10f);
                TestFields.Set(c, "burnDuration", 0.4f);
                TestFields.Set(c, "disableWhenConsumed", true);
            });
            GameObject host = crate.gameObject;

            Send(crate, MagicElement.Heat, 12f);
            Assert.IsTrue(crate.IsBurning);

            yield return WaitUntil(() => !host.activeSelf, 2f, "the crate to burn away");
        }

        [UnityTest]
        public IEnumerator WeakHeatCoolsOffInsteadOfAddingUpForever()
        {
            FlammableObject crate = Spawn<FlammableObject>("Crate", c =>
            {
                TestFields.Set(c, "ignitionThreshold", 10f);
                TestFields.Set(c, "coolingPerSecond", 20f);
            });

            Send(crate, MagicElement.Heat, 6f);
            yield return Wait(0.8f);
            Send(crate, MagicElement.Heat, 6f);

            Assert.IsFalse(crate.IsBurning,
                "Two weak hits a second apart must not light it, or cooling means nothing.");
        }
    }
}
