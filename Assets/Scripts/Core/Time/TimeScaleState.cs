using System;

namespace Frieren.Core.Timing
{
    /// <summary>
    /// The arithmetic behind the time scale: a base that game state owns, a dip that expires, and a
    /// hold that lasts until someone lets go.
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

        /// <summary>
        /// A sustained slow with no end time, for something the player is holding open.
        /// </summary>
        /// <remarks>
        /// Separate from the dip because it is a different shape of thing. A dip is an event with a
        /// duration and expires by itself; a hold is a state that lasts until it is released, and
        /// re-requesting a dip every frame to fake one would fight the dip's own "harder wins" rule
        /// and leave time slowed for a few frames after release.
        /// </remarks>
        public float HoldFactor { get; private set; } = 1f;

        public bool IsDipping => DipFactor < 1f;

        public bool IsHolding => HoldFactor < 1f;

        /// <summary>What <c>Time.timeScale</c> should be right now.</summary>
        /// <remarks>
        /// The base multiplies, so a pause always wins outright. The dip and the hold take the
        /// stronger of the two rather than multiplying: two slows that compound are hard to reason
        /// about and produce a near-freeze the moment a hit lands while a menu is open, which is
        /// nobody's intent.
        /// </remarks>
        public float Scale => BaseScale * Math.Min(DipFactor, HoldFactor);

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

        /// <summary>Sets the sustained slow. 1 is normal speed.</summary>
        public void SetHold(float factor)
        {
            HoldFactor = factor < 0f ? 0f : factor > 1f ? 1f : factor;
        }

        public void ClearHold() => HoldFactor = 1f;

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
