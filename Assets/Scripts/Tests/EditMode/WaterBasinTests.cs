using Frieren.Core.Magic;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// The basin exists to prove the design pillar - two spells combining into a route no one
    /// scripted - so its state machine is worth pinning down.
    /// </summary>
    public sealed class WaterBasinTests
    {
        private const float Fill = 20f;
        private const float Freeze = 15f;
        private const float Heat = 15f;

        private GameObject host;
        private WaterBasin basin;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Basin");
            basin = host.AddComponent<WaterBasin>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        private bool Send(MagicElement element, float magnitude) =>
            basin.ReceiveMagic(new MagicPulse(element, magnitude, Vector3.zero));

        [Test]
        public void StartsEmpty() => Assert.AreEqual(BasinState.Empty, basin.State);

        [Test]
        public void WaterFillsItOnceEnoughArrives()
        {
            Assert.IsTrue(Send(MagicElement.Water, Fill * 0.5f));
            Assert.AreEqual(BasinState.Empty, basin.State, "Half a basin is not a full one.");

            Assert.IsTrue(Send(MagicElement.Water, Fill * 0.5f));
            Assert.AreEqual(BasinState.Filled, basin.State);
        }

        [Test]
        public void ChillingAnEmptyBasinDoesNothing()
        {
            Assert.IsFalse(Send(MagicElement.Cold, 100f));
            Assert.AreEqual(BasinState.Empty, basin.State);
        }

        [Test]
        public void ColdFreezesAFullBasin()
        {
            Send(MagicElement.Water, Fill);

            Assert.IsTrue(Send(MagicElement.Cold, Freeze));
            Assert.AreEqual(BasinState.Frozen, basin.State);
        }

        [Test]
        public void HeatThawsIceBackToWaterRatherThanBoilingItAway()
        {
            Send(MagicElement.Water, Fill);
            Send(MagicElement.Cold, Freeze);

            Send(MagicElement.Heat, Heat);
            Assert.AreEqual(BasinState.Filled, basin.State);

            Send(MagicElement.Heat, Heat);
            Assert.AreEqual(BasinState.Empty, basin.State);
        }

        [Test]
        public void MoreWaterIntoAFullBasinIsWasted()
        {
            Send(MagicElement.Water, Fill);

            Assert.IsFalse(Send(MagicElement.Water, Fill));
            Assert.AreEqual(BasinState.Filled, basin.State);
        }

        [Test]
        public void UnrelatedElementsAreIgnored()
        {
            Assert.IsFalse(Send(MagicElement.Unbinding, 100f));
            Assert.IsFalse(Send(MagicElement.Arcane, 100f));
        }

        [Test]
        public void WarmingThenChillingDoesNotLeaveProgressBehind()
        {
            Send(MagicElement.Water, Fill);
            Send(MagicElement.Heat, Heat - 1f);
            Send(MagicElement.Cold, Freeze - 1f);

            Assert.AreEqual(BasinState.Filled, basin.State);

            // The warmth was cleared by the cold, so one more heat pulse must not tip it over.
            Send(MagicElement.Heat, 1f);
            Assert.AreEqual(BasinState.Filled, basin.State);
        }
    }
}
