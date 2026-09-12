using Frieren.Core.Timing;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// A bug in here is a game that will not unpause, which has already happened once on this
    /// project by a different route. The pause-and-dip interactions are the tests that matter.
    /// </summary>
    public sealed class TimeScaleStateTests
    {
        private TimeScaleState state;

        [SetUp]
        public void SetUp() => state = new TimeScaleState();

        [Test]
        public void StartsAtNormalSpeed()
        {
            Assert.AreEqual(1f, state.Scale, 0.0001f);
            Assert.IsFalse(state.IsDipping);
        }

        [Test]
        public void TheBaseScaleSetsTheSpeed()
        {
            state.BaseScale = 0f;

            Assert.AreEqual(0f, state.Scale, 0.0001f);
        }

        [Test]
        public void ANegativeBaseScaleIsClamped()
        {
            state.BaseScale = -3f;

            Assert.AreEqual(0f, state.BaseScale, 0.0001f);
        }

        [Test]
        public void ADipMultipliesTheBase()
        {
            state.RequestDip(0.25f, 1f);

            Assert.AreEqual(0.25f, state.Scale, 0.0001f);
        }

        [Test]
        public void ADipExpiresOnItsOwn()
        {
            state.RequestDip(0.1f, 1f);
            Assert.IsTrue(state.Tick(1.01f));

            Assert.AreEqual(1f, state.Scale, 0.0001f);
            Assert.IsFalse(state.IsDipping);
        }

        [Test]
        public void ADipDoesNotExpireEarly()
        {
            state.RequestDip(0.1f, 1f);

            Assert.IsFalse(state.Tick(0.99f));
            Assert.IsTrue(state.IsDipping);
        }

        [Test]
        public void AStrongerDipTakesOver()
        {
            state.RequestDip(0.5f, 1f);
            state.RequestDip(0.1f, 1f);

            Assert.AreEqual(0.1f, state.Scale, 0.0001f);
        }

        [Test]
        public void AWeakerShorterDipDoesNotSoftenARunningOne()
        {
            state.RequestDip(0.1f, 2f);
            state.RequestDip(0.8f, 1f);

            Assert.AreEqual(0.1f, state.Scale, 0.0001f,
                "Two hits together should read as one heavy hit, not as the second cancelling the first.");
            Assert.AreEqual(2f, state.DipEndsAt, 0.0001f);
        }

        [Test]
        public void AWeakerButLongerDipStillExtendsTheEnd()
        {
            state.RequestDip(0.1f, 1f);
            state.RequestDip(0.8f, 3f);

            Assert.AreEqual(0.1f, state.Scale, 0.0001f, "The harder factor is kept.");
            Assert.AreEqual(3f, state.DipEndsAt, 0.0001f, "The later end is kept.");
        }

        [Test]
        public void ADipDuringAPauseChangesNothingVisible()
        {
            state.BaseScale = 0f;
            state.RequestDip(0.1f, 1f);

            Assert.AreEqual(0f, state.Scale, 0.0001f);
        }

        [Test]
        public void ADipExpiringDuringAPauseDoesNotUnpause()
        {
            state.BaseScale = 0f;
            state.RequestDip(0.1f, 1f);
            state.Tick(2f);

            Assert.AreEqual(0f, state.Scale, 0.0001f,
                "This is the whole reason the two are separate factors.");
        }

        [Test]
        public void UnpausingAfterADipExpiredRestoresFullSpeed()
        {
            state.RequestDip(0.1f, 1f);
            state.BaseScale = 0f;
            state.Tick(2f);
            state.BaseScale = 1f;

            Assert.AreEqual(1f, state.Scale, 0.0001f);
        }

        [Test]
        public void UnpausingMidDipKeepsTheDip()
        {
            state.RequestDip(0.2f, 5f);
            state.BaseScale = 0f;
            state.BaseScale = 1f;

            Assert.AreEqual(0.2f, state.Scale, 0.0001f);
        }

        [Test]
        public void ClearDipRestoresSpeedWithoutTouchingTheBase()
        {
            state.BaseScale = 0.5f;
            state.RequestDip(0.1f, 5f);
            state.ClearDip();

            Assert.AreEqual(0.5f, state.Scale, 0.0001f);
        }

        [TestCase(-1f, 0f)]
        [TestCase(2f, 1f)]
        public void DipFactorsAreClamped(float requested, float expected)
        {
            state.RequestDip(requested, 1f);

            Assert.AreEqual(expected, state.DipFactor, 0.0001f);
        }

        [Test]
        public void TickReportsNoChangeWhenNothingIsDipping()
        {
            Assert.IsFalse(state.Tick(100f));
        }
    }
}
