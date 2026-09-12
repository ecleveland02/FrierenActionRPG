using Frieren.Magic;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class SpellCooldownTrackerTests
    {
        private SpellCooldownTracker tracker;

        [SetUp]
        public void SetUp() => tracker = new SpellCooldownTracker();

        [Test]
        public void AnUnusedSpellIsReady()
        {
            Assert.IsTrue(tracker.IsReady("spell.fire", 0f));
        }

        [Test]
        public void BeginningACooldownBlocksTheSpell()
        {
            tracker.Begin("spell.fire", now: 10f, cooldown: 2f);

            Assert.IsFalse(tracker.IsReady("spell.fire", 10f));
            Assert.IsFalse(tracker.IsReady("spell.fire", 11.9f));
        }

        [Test]
        public void TheSpellIsReadyAgainExactlyWhenTheCooldownElapses()
        {
            tracker.Begin("spell.fire", now: 10f, cooldown: 2f);

            Assert.IsTrue(tracker.IsReady("spell.fire", 12f));
        }

        [Test]
        public void CooldownsAreTrackedPerSpell()
        {
            tracker.Begin("spell.fire", now: 0f, cooldown: 5f);

            Assert.IsFalse(tracker.IsReady("spell.fire", 1f));
            Assert.IsTrue(tracker.IsReady("spell.ice", 1f), "One spell cooling must not block another.");
        }

        [Test]
        public void RemainingCountsDownAndNeverGoesNegative()
        {
            tracker.Begin("spell.fire", now: 0f, cooldown: 3f);

            Assert.AreEqual(3f, tracker.RemainingFor("spell.fire", 0f), 0.001f);
            Assert.AreEqual(1f, tracker.RemainingFor("spell.fire", 2f), 0.001f);
            Assert.AreEqual(0f, tracker.RemainingFor("spell.fire", 99f), 0.001f);
        }

        [Test]
        public void AZeroCooldownLeavesTheSpellReady()
        {
            tracker.Begin("spell.bolt", now: 5f, cooldown: 0f);

            Assert.IsTrue(tracker.IsReady("spell.bolt", 5f));
            Assert.AreEqual(0f, tracker.RemainingFor("spell.bolt", 5f));
        }

        [Test]
        public void RecastingRestartsTheCooldownRatherThanExtendingIt()
        {
            tracker.Begin("spell.fire", now: 0f, cooldown: 5f);
            tracker.Begin("spell.fire", now: 4f, cooldown: 5f);

            Assert.AreEqual(5f, tracker.RemainingFor("spell.fire", 4f), 0.001f);
        }

        [Test]
        public void ClearingMakesEverythingReady()
        {
            tracker.Begin("spell.fire", now: 0f, cooldown: 10f);
            tracker.Begin("spell.ice", now: 0f, cooldown: 10f);

            tracker.Clear();

            Assert.IsTrue(tracker.IsReady("spell.fire", 1f));
            Assert.IsTrue(tracker.IsReady("spell.ice", 1f));
        }

        [Test]
        public void ClearingOneSpellLeavesTheOthers()
        {
            tracker.Begin("spell.fire", now: 0f, cooldown: 10f);
            tracker.Begin("spell.ice", now: 0f, cooldown: 10f);

            tracker.Clear("spell.fire");

            Assert.IsTrue(tracker.IsReady("spell.fire", 1f));
            Assert.IsFalse(tracker.IsReady("spell.ice", 1f));
        }

        [Test]
        public void AnEmptyIdIsNeverReadyAndIsNeverStored()
        {
            Assert.IsFalse(tracker.IsReady(null, 0f));
            Assert.IsFalse(tracker.IsReady(string.Empty, 0f));

            Assert.DoesNotThrow(() => tracker.Begin(null, 0f, 5f));
            Assert.AreEqual(0f, tracker.RemainingFor(null, 0f));
        }
    }
}
