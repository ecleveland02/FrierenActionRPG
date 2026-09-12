using System;
using UnityEngine;

namespace Frieren.Core.Timing
{
    /// <summary>
    /// The one place <see cref="UnityEngine.Time.timeScale"/> is written.
    /// </summary>
    /// <remarks>
    /// Three unrelated things want to slow time down: the pause state, which stops it entirely and
    /// indefinitely; hit-stop, which dips it for a few frames on impact; and a held slow, for
    /// something the player has open. All of them writing <c>Time.timeScale</c> directly is a bug
    /// waiting to happen - a hit landing a frame before a
    /// pause restores the scale to 1 when its dip expires, and the game unpauses itself. That is
    /// the same shape as the pause defect found in Milestone 1, and it is worth not repeating.
    ///
    /// So the two are separate inputs that multiply, and <see cref="TimeScaleState"/> holds the
    /// arithmetic without any Unity types so it can be tested. This class only supplies the clock
    /// and applies the answer.
    ///
    /// Registered in <c>ServiceLocator</c> rather than reached through a singleton, and every caller
    /// treats it as optional - a missing time service means time simply runs at normal speed.
    /// </remarks>
    public sealed class TimeScaleService
    {
        private readonly TimeScaleState state = new TimeScaleState();

        /// <summary>What game state wants time to run at. 0 while paused.</summary>
        public float BaseScale
        {
            get => state.BaseScale;
            set
            {
                state.BaseScale = value;
                Apply();
            }
        }

        public bool IsDipping => state.IsDipping;

        public bool IsHolding => state.IsHolding;

        /// <summary>Whatever currently holds time slowed, or null.</summary>
        public object HoldOwner { get; private set; }

        /// <summary>
        /// Slows time briefly, for impact. Measured in unscaled seconds, because a dip timed in
        /// scaled seconds would slow its own expiry down and last far longer than asked.
        /// </summary>
        /// <param name="factor">0 to 1. Lower is a harder stop.</param>
        /// <param name="realtimeSeconds">How long, in real seconds.</param>
        public void RequestDip(float factor, float realtimeSeconds)
        {
            float endsAt = UnityEngine.Time.realtimeSinceStartup + Mathf.Max(0f, realtimeSeconds);
            state.RequestDip(factor, endsAt);
            Apply();
        }

        /// <summary>Ends any dip immediately. For pausing, scene changes and tests.</summary>
        public void ClearDip()
        {
            state.ClearDip();
            Apply();
        }

        /// <summary>
        /// Claims a sustained slow until <see cref="ReleaseHold"/>. Returns false if something else
        /// already holds it.
        /// </summary>
        /// <remarks>
        /// A single-holder claim with a named owner, the same shape as <c>CharacterActionLock</c>
        /// and for the same reason: if two systems both want time slowed at once, that is a design
        /// decision somebody should make deliberately, not something that should fall out of
        /// whichever one asked last.
        ///
        /// The owner matters on release. A holder that could be cancelled by anyone is a holder
        /// that eventually leaves the game running at a third speed with nothing to blame.
        /// </remarks>
        public bool TryHold(object owner, float factor)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (HoldOwner != null && !ReferenceEquals(HoldOwner, owner))
            {
                return false;
            }

            HoldOwner = owner;
            state.SetHold(factor);
            Apply();
            return true;
        }

        /// <summary>Releases the hold. Ignored unless <paramref name="owner"/> is the holder.</summary>
        public bool ReleaseHold(object owner)
        {
            if (owner == null || !ReferenceEquals(HoldOwner, owner))
            {
                return false;
            }

            HoldOwner = null;
            state.ClearHold();
            Apply();
            return true;
        }

        /// <summary>
        /// Drops the hold whatever owns it. For scene changes, where the holder may already be gone.
        /// </summary>
        public void ForceClearHold()
        {
            HoldOwner = null;
            state.ClearHold();
            Apply();
        }

        /// <summary>
        /// Expires a finished dip. Called from an update that still runs at a zero time scale, so a
        /// dip cannot outlive a pause it overlapped.
        /// </summary>
        public void Tick()
        {
            if (state.Tick(UnityEngine.Time.realtimeSinceStartup))
            {
                Apply();
            }
        }

        private void Apply() => UnityEngine.Time.timeScale = state.Scale;
    }
}
