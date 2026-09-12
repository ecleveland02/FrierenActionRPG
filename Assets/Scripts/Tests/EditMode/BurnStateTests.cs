using Frieren.World;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class BurnStateTests
    {
        private const float Threshold = 10f;
        private const float Duration = 5f;
        private const float Cooling = 2f;

        private BurnState state;

        [SetUp]
        public void SetUp() => state = new BurnState(Threshold, Duration, Cooling);

        [Test]
        public void StartsCold()
        {
            Assert.IsFalse(state.IsBurning);
            Assert.IsFalse(state.IsConsumed);
            Assert.AreEqual(0f, state.Heat);
        }

        [Test]
        public void HeatBelowTheThresholdDoesNotIgnite()
        {
            Assert.IsFalse(state.AddHeat(9f));
            Assert.IsFalse(state.IsBurning);
            Assert.AreEqual(0.9f, state.HeatProgress, 0.001f);
        }

        [Test]
        public void HeatAccumulatesAcrossSeveralHits()
        {
            state.AddHeat(6f);

            Assert.IsTrue(state.AddHeat(6f), "Two hits that together exceed the threshold must ignite it.");
            Assert.IsTrue(state.IsBurning);
        }

        [Test]
        public void IgnitionIsReportedExactlyOnce()
        {
            Assert.IsTrue(state.AddHeat(Threshold));
            Assert.IsFalse(state.AddHeat(Threshold), "Something already alight must not re-ignite.");
        }

        [Test]
        public void HeatBleedsAwaySoWeakHitsDoNotEventuallyAddUp()
        {
            state.AddHeat(6f);
            state.Tick(2f);

            Assert.AreEqual(2f, state.Heat, 0.001f);
            Assert.IsFalse(state.AddHeat(6f), "After cooling, the same second hit should no longer ignite.");
        }

        [Test]
        public void ColdRemovesProgressTowardIgnition()
        {
            state.AddHeat(9f);
            state.AddCold(5f);

            Assert.AreEqual(4f, state.Heat, 0.001f);
            Assert.IsFalse(state.AddHeat(5f), "Alternating heat and cold must not still light it.");
        }

        [Test]
        public void ColdPutsOutAFireAndReportsItOnce()
        {
            state.AddHeat(Threshold);

            Assert.IsTrue(state.AddCold(1f));
            Assert.IsFalse(state.IsBurning);
            Assert.IsFalse(state.AddCold(1f), "Extinguishing twice must only report once.");
        }

        [Test]
        public void ExtinguishingLeavesItRelightable()
        {
            state.AddHeat(Threshold);
            state.AddCold(1f);

            Assert.IsTrue(state.AddHeat(Threshold));
            Assert.IsFalse(state.IsConsumed);
        }

        [Test]
        public void BurningOutIsReportedOnceAndConsumesIt()
        {
            state.AddHeat(Threshold);

            Assert.IsFalse(state.Tick(Duration - 0.1f));
            Assert.IsTrue(state.Tick(0.2f));
            Assert.IsTrue(state.IsConsumed);
            Assert.IsFalse(state.IsBurning);
            Assert.IsFalse(state.Tick(1f), "Burning out must only report once.");
        }

        [Test]
        public void ConsumedThingsCannotBeRelit()
        {
            state.AddHeat(Threshold);
            state.Tick(Duration + 1f);

            Assert.IsFalse(state.AddHeat(999f));
            Assert.IsFalse(state.IsBurning);
        }

        [Test]
        public void BurnProgressRunsFromZeroToOne()
        {
            state.AddHeat(Threshold);

            Assert.AreEqual(0f, state.BurnProgress, 0.001f);
            state.Tick(Duration * 0.5f);
            Assert.AreEqual(0.5f, state.BurnProgress, 0.01f);
        }

        [Test]
        public void NonPositiveInputsDoNothing()
        {
            Assert.IsFalse(state.AddHeat(0f));
            Assert.IsFalse(state.AddHeat(-5f));
            Assert.IsFalse(state.AddCold(0f));
            Assert.IsFalse(state.Tick(0f));
            Assert.AreEqual(0f, state.Heat);
        }

        [Test]
        public void ResetReturnsItToUnburnt()
        {
            state.AddHeat(Threshold);
            state.Tick(Duration + 1f);

            state.Reset();

            Assert.IsFalse(state.IsConsumed);
            Assert.IsTrue(state.AddHeat(Threshold));
        }
    }
}
