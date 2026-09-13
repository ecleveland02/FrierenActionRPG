using System;

namespace Frieren.UI
{
    /// <summary>
    /// The two-speed bar: a fill that snaps and a ghost behind it that catches up.
    /// </summary>
    /// <remarks>
    /// Plain C# because this is the part of a health bar that is worth getting right and impossible
    /// to eyeball. A bar that only interpolates hides how big a hit was: by the time the eye finds
    /// it, it has already finished moving. Two bars fix that. The front one is the truth and moves
    /// immediately, so the new value is never a lie; the ghost trails behind at a fixed rate, and
    /// the gap between them is a readable measure of what just happened.
    ///
    /// The ghost only lags on the way down. Healing that crept up behind the real value would show
    /// the player less health than they have, which is the one direction a health bar must never be
    /// wrong in.
    ///
    /// Rate is per second in bar fractions, not a lerp factor, so the trail takes the same wall
    /// clock time whatever the frame rate and whatever the bar's maximum happens to be.
    /// </remarks>
    public static class BarSmoothing
    {
        /// <summary>
        /// Advances a trailing value toward the real one.
        /// </summary>
        /// <param name="ghost">Where the trailing bar is now, 0..1.</param>
        /// <param name="actual">Where the real bar is, 0..1.</param>
        /// <param name="deltaTime">Seconds since the last step. Negative is treated as zero.</param>
        /// <param name="ratePerSecond">Fractions of the bar the ghost closes per second.</param>
        /// <param name="delayRemaining">
        /// Seconds still to wait before the ghost starts moving, decremented by this call. The
        /// pause is what makes the gap legible; without it the trail begins closing before the eye
        /// has arrived.
        /// </param>
        public static float Step(float ghost, float actual, float deltaTime, float ratePerSecond,
            ref float delayRemaining)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            // Gaining ground: the ghost has nothing to say, so it keeps up exactly.
            if (actual >= ghost)
            {
                delayRemaining = 0f;
                return actual;
            }

            if (delayRemaining > 0f)
            {
                delayRemaining = Math.Max(0f, delayRemaining - deltaTime);
                return ghost;
            }

            float step = Math.Max(0f, ratePerSecond) * deltaTime;
            return Math.Max(actual, ghost - step);
        }

        /// <summary>
        /// Clamps a normalised value and turns a NaN into zero rather than into a bar of NaN width.
        /// </summary>
        /// <remarks>
        /// A resource with a maximum of zero divides by zero somewhere upstream, and a NaN passed to
        /// a RectTransform makes the whole element vanish with no error, which is a miserable thing
        /// to track down from a screenshot.
        /// </remarks>
        public static float Safe(float normalized)
        {
            if (float.IsNaN(normalized))
            {
                return 0f;
            }

            return normalized < 0f ? 0f : normalized > 1f ? 1f : normalized;
        }
    }
}
