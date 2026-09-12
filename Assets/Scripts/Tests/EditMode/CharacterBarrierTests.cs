using Frieren.Characters;
using Frieren.Core.Magic;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// The barrier is a MonoBehaviour, but everything that matters about it is decided in
    /// <c>ReceiveMagic</c> and <c>ModifyIncomingDamage</c>, neither of which needs a running frame.
    /// Only lapsing does, and that is the one thing these cannot cover.
    /// </summary>
    public sealed class CharacterBarrierTests
    {
        private const float Capacity = 60f;
        private const float WardThreshold = 5f;

        private GameObject host;
        private CharacterBarrier barrier;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("BarrierHost");
            barrier = host.AddComponent<CharacterBarrier>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        private static MagicPulse Ward(float magnitude) =>
            new MagicPulse(MagicElement.Warding, magnitude, Vector3.zero);

        [Test]
        public void StartsDown()
        {
            Assert.IsFalse(barrier.IsUp);
            Assert.AreEqual(0f, barrier.Remaining);
        }

        [Test]
        public void AWeakPulseDoesNotRaiseIt()
        {
            Assert.IsFalse(barrier.ReceiveMagic(Ward(WardThreshold - 1f)));
            Assert.IsFalse(barrier.IsUp);
        }

        [Test]
        public void AWardingPulseRaisesItToFull()
        {
            Assert.IsTrue(barrier.ReceiveMagic(Ward(WardThreshold)));
            Assert.IsTrue(barrier.IsUp);
            Assert.AreEqual(Capacity, barrier.Remaining, 0.001f);
        }

        [Test]
        public void OtherElementsAreIgnored()
        {
            Assert.IsFalse(barrier.ReceiveMagic(new MagicPulse(MagicElement.Heat, 100f, Vector3.zero)));
            Assert.IsFalse(barrier.IsUp);
        }

        [Test]
        public void ItAbsorbsDamageUpToWhatIsLeft()
        {
            barrier.ReceiveMagic(Ward(WardThreshold));

            float through = barrier.ModifyIncomingDamage(new DamageInfo(20f), 20f);

            Assert.AreEqual(0f, through, 0.001f, "A barrier at full strength must stop a small hit outright.");
            Assert.AreEqual(Capacity - 20f, barrier.Remaining, 0.001f);
        }

        [Test]
        public void DamageBeyondCapacityCarriesOn()
        {
            barrier.ReceiveMagic(Ward(WardThreshold));

            float through = barrier.ModifyIncomingDamage(new DamageInfo(Capacity + 15f), Capacity + 15f);

            Assert.AreEqual(15f, through, 0.001f);
            Assert.IsFalse(barrier.IsUp, "Absorbing more than it holds must break it.");
        }

        [Test]
        public void RefreshingDoesNotStack()
        {
            barrier.ReceiveMagic(Ward(WardThreshold));
            barrier.ModifyIncomingDamage(new DamageInfo(30f), 30f);
            barrier.ReceiveMagic(Ward(WardThreshold));

            Assert.AreEqual(Capacity, barrier.Remaining, 0.001f,
                "A held barrier refreshes to full; it must never accumulate past capacity.");
        }

        [Test]
        public void TrueDamageIgnoresIt()
        {
            barrier.ReceiveMagic(Ward(WardThreshold));

            float through = barrier.ModifyIncomingDamage(new DamageInfo(10f, DamageType.True), 10f);

            Assert.AreEqual(10f, through, 0.001f);
            Assert.AreEqual(Capacity, barrier.Remaining, 0.001f, "An ignored type must not spend the ward.");
        }

        [Test]
        public void ADownBarrierChangesNothing()
        {
            Assert.AreEqual(25f, barrier.ModifyIncomingDamage(new DamageInfo(25f), 25f), 0.001f);
        }

        [Test]
        public void DispelDropsItImmediately()
        {
            barrier.ReceiveMagic(Ward(WardThreshold));
            barrier.Dispel();

            Assert.IsFalse(barrier.IsUp);
            Assert.AreEqual(25f, barrier.ModifyIncomingDamage(new DamageInfo(25f), 25f), 0.001f);
        }
    }
}
