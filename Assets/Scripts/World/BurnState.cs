using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// Whether something is hot, alight, or burnt out.
    /// </summary>
    /// <remarks>
    /// Plain C# so the state machine can be tested without a scene or a clock. The interesting parts
    /// are the transitions - heat accumulating and then bleeding away, cold putting a fire out,
    /// something already consumed refusing to light again - and every one of them is a timing
    /// question that is tedious to confirm by setting crates on fire and easy to confirm in a test.
    /// </remarks>
    public sealed class BurnState
    {
        private readonly float ignitionThreshold;
        private readonly float burnDuration;
        private readonly float coolingPerSecond;

        private float burnRemaining;

        public BurnState(float ignitionThreshold, float burnDuration, float coolingPerSecond)
        {
            this.ignitionThreshold = Mathf.Max(0.01f, ignitionThreshold);
            this.burnDuration = Mathf.Max(0f, burnDuration);
            this.coolingPerSecond = Mathf.Max(0f, coolingPerSecond);
        }

        /// <summary>Heat accumulated but not yet enough to ignite. Bleeds away over time.</summary>
        public float Heat { get; private set; }

        public bool IsBurning { get; private set; }

        /// <summary>True once it has finished burning. Consumed things cannot be relit.</summary>
        public bool IsConsumed { get; private set; }

        /// <summary>0 to 1 through the burn. Useful for driving a visual.</summary>
        public float BurnProgress => burnDuration <= 0f ? 1f : 1f - Mathf.Clamp01(burnRemaining / burnDuration);

        /// <summary>How close to catching alight, 0 to 1. What a warming-up visual should read.</summary>
        public float HeatProgress => Mathf.Clamp01(Heat / ignitionThreshold);

        /// <summary>Adds heat. Returns true only on the frame it ignites.</summary>
        public bool AddHeat(float amount)
        {
            if (amount <= 0f || IsConsumed || IsBurning)
            {
                return false;
            }

            Heat += amount;

            if (Heat < ignitionThreshold)
            {
                return false;
            }

            Heat = 0f;
            IsBurning = true;
            burnRemaining = burnDuration;
            return true;
        }

        /// <summary>Applies cold. Returns true only on the frame it puts a fire out.</summary>
        public bool AddCold(float amount)
        {
            if (amount <= 0f)
            {
                return false;
            }

            if (!IsBurning)
            {
                // Cooling something that is merely warm should also undo progress toward ignition,
                // otherwise heat and cold applied alternately still lights it.
                Heat = Mathf.Max(0f, Heat - amount);
                return false;
            }

            IsBurning = false;
            burnRemaining = 0f;
            Heat = 0f;
            return true;
        }

        /// <summary>Advances time. Returns true only on the frame it burns out.</summary>
        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return false;
            }

            if (!IsBurning)
            {
                Heat = Mathf.Max(0f, Heat - coolingPerSecond * deltaTime);
                return false;
            }

            burnRemaining -= deltaTime;

            if (burnRemaining > 0f)
            {
                return false;
            }

            IsBurning = false;
            IsConsumed = true;
            burnRemaining = 0f;
            return true;
        }

        /// <summary>Returns to unburnt. For respawning and for restoring a save.</summary>
        public void Reset()
        {
            Heat = 0f;
            burnRemaining = 0f;
            IsBurning = false;
            IsConsumed = false;
        }
    }
}
