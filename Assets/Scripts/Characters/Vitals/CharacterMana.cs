using System;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// A character's mana, and the gate on whether a spell can be paid for.
    /// </summary>
    /// <remarks>
    /// Spending is all-or-nothing. A spell that takes what mana there is and then fails is worse
    /// than one that never starts, and <see cref="TrySpend"/> being the only way to pay means no
    /// caller can accidentally implement the partial version.
    ///
    /// The regeneration delay after spending is the main lever on casting pace, which is why it is
    /// authored per archetype rather than being a constant here.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterMana : CharacterResource
    {
        /// <summary>Raised when mana is successfully spent, with the amount.</summary>
        public event Action<float> Spent;

        /// <summary>Raised when a spend was refused for want of mana, with the amount needed.</summary>
        public event Action<float> SpendFailed;

        protected override float MaxFromStats => Stats != null ? Stats.MaxMana : 0f;

        protected override float RegenPerSecond => Stats != null ? Stats.ManaRegenPerSecond : 0f;

        protected override float RegenDelaySeconds => Stats != null ? Stats.ManaRegenDelay : 0f;

        public bool CanAfford(float cost) => cost <= 0f || Current >= cost;

        /// <summary>
        /// Pays <paramref name="cost"/> in full, or pays nothing and returns false. A free spell
        /// (zero or negative cost) always succeeds and does not start the regeneration delay.
        /// </summary>
        public bool TrySpend(float cost)
        {
            if (cost <= 0f)
            {
                return true;
            }

            if (!TryDrainExactly(cost))
            {
                SpendFailed?.Invoke(cost);
                return false;
            }

            Spent?.Invoke(cost);
            return true;
        }

        public float Restore(float amount) => Replenish(amount);
    }
}
