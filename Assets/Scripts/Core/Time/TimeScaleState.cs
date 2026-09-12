using System;

namespace Frieren.Core.Timing
{
    /// <summary>
    /// The arithmetic behind the time scale: a base that game state owns, and a dip that expires.
    /// </summary>
    /// <remarks>
    /// Plain C# with no Unity types, so it can be tested. That matters more here than usual: a bug
    /// in this class is a game that will not unpause, and the only alternative test would have to
    /// write the real <c>Time.timeScale</c> - which in edit mode is the editor's own clock, and a
    /// test that leaves it at zero leaves the editor at zero.
    ///
    /// <see cref="TimeScaleService"/> is the thin adapter that feeds it a clock and applies the
    /// result. Same split as <c>JumpGate</c>, <c>MotorMath</c> and <c>BurnState</c>.
    /// </remarks>
    public sealed class TimeScaleState
    {
        private float baseScale = 1f;

        /// <summary>What game state wants time to run at. 0 while paused.</summary>
        public float BaseScale
        {
            get => baseScale;
            set => baseScale = Math.Max(0f, value);
        }

        public float DipFactor { get; private set; } = 1f;

        public float DipEndsAt { get; private set; }

        public bool IsDipping => DipFactor < 1f;

        /// <summary>What <c>Time.timeScale</c> should be right now.</summary>
        public float Scale => BaseScale * DipFactor;

        /// <summary>
        /// Requests a dip ending at <paramref name="endsAt"/> on the same clock <see cref="Tick"/>
        /// is given.
        /// </summary>
        /// <remarks>
        /// A weaker, shorter dip never displaces a stronger, longer one. Two hits landing together
        /// should read as one heavy hit rather than the second one softening the first, and without
        /// this rule a stream of small hits keeps cutting a big one short.
        /// </remarks>
        public void RequestDip(float factor, float endsAt)
        {
            factor = factor < 0f ? 0f : factor > 1f ? 1f : factor;

            if (IsDipping && factor > DipFactor && endsAt < DipEndsAt)
            {
                return;
            }

            DipFactor = Math.Min(DipFactor, factor);
            DipEndsAt = Math.Max(DipEndsAt, endsAt);
        }

        public void ClearDip()
        {
            DipFactor = 1f;
            DipEndsAt = 0f;
        }

        /// <summary>Expires a finished dip. Returns true if anything changed.</summary>
        public bool Tick(float now)
        {
            if (!IsDipping || now < DipEndsAt)
            {
                return false;
            }

            ClearDip();
            return true;
        }
    }
}
