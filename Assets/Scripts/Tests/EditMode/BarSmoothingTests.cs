using Frieren.UI;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// The trailing health bar. Pure arithmetic, and the half of a health bar that can be wrong
    /// without anyone noticing until it matters.
    /// </summary>
    public sealed class BarSmoothingTests
    {
        [Test]
        public void GainingGroundKeepsUpImmediately()
        {
            float delay = 5f;

            Assert.AreEqual(0.9f, BarSmoothing.Step(0.4f, 0.9f, 0.016f, 0.5f, ref delay), 0.0001f,
                "Healing must never show less health than the player has.");
            Assert.AreEqual(0f, delay, "Recovering cancels a pending drain.");
        }

        [Test]
        public void TheGhostHoldsStillWhileTheDelayRuns()
        {
            float delay = 0.3f;
            float ghost = BarSmoothing.Step(1f, 0.4f, 0.1f, 0.5f, ref delay);

            Assert.AreEqual(1f, ghost, 0.0001f, "The gap has to be visible before it starts closing.");
            Assert.AreEqual(0.2f, delay, 0.0001f);
        }

        [Test]
        public void TheGhostClosesAtTheGivenRateOnceTheDelayExpires()
        {
            float delay = 0f;

            // Half a bar per second, for a fifth of a second.
            Assert.AreEqual(0.9f, BarSmoothing.Step(1f, 0.2f, 0.2f, 0.5f, ref delay), 0.0001f);
        }

        [Test]
        public void TheGhostNeverOvershootsTheRealValue()
        {
            float delay = 0f;

            Assert.AreEqual(0.4f, BarSmoothing.Step(0.45f, 0.4f, 10f, 0.5f, ref delay), 0.0001f,
                "A long frame must land on the real value, not below it.");
        }

        [Test]
        public void ANegativeDeltaTimeDoesNotMoveTheBarBackwards()
        {
            float delay = 0f;

            Assert.AreEqual(1f, BarSmoothing.Step(1f, 0.2f, -5f, 0.5f, ref delay), 0.0001f);
        }

        [Test]
        public void ANegativeRateIsTreatedAsFrozenRatherThanAsRefilling()
        {
            float delay = 0f;

            Assert.AreEqual(1f, BarSmoothing.Step(1f, 0.2f, 0.2f, -3f, ref delay), 0.0001f);
        }

        [Test]
        public void SafeClampsOutOfRangeValues()
        {
            Assert.AreEqual(0f, BarSmoothing.Safe(-0.5f));
            Assert.AreEqual(1f, BarSmoothing.Safe(4f));
            Assert.AreEqual(0.25f, BarSmoothing.Safe(0.25f));
        }

        [Test]
        public void SafeTurnsNaNIntoZeroRatherThanIntoAnInvisibleElement()
        {
            // A resource with a zero maximum divides by zero upstream, and a NaN width makes a
            // RectTransform disappear with no error at all.
            Assert.AreEqual(0f, BarSmoothing.Safe(float.NaN));
        }
    }
}
