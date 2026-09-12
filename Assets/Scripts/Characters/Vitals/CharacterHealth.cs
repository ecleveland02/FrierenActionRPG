using System;
using Frieren.Core.Debugging;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// A character's health, and whether it is still alive.
    /// </summary>
    /// <remarks>
    /// Death is an event, not a behaviour. This component does not disable input, play an
    /// animation, drop loot or despawn anything - it reports that health reached zero and lets
    /// whoever cares decide. The player and an enemy want completely different things to happen,
    /// and putting either here would mean the other fights it.
    ///
    /// A dead character ignores further damage. Without that, a corpse hit by three more arrows
    /// raises <see cref="Died"/> three more times, and anything counting kills counts wrong.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterHealth : CharacterResource
    {
        /// <summary>Raised for each hit that actually removed health, with the amount taken.</summary>
        public event Action<DamageInfo, float> Damaged;

        /// <summary>Raised once, when health first reaches zero.</summary>
        public event Action<DamageInfo> Died;

        public bool IsAlive { get; private set; } = true;

        protected override float MaxFromStats => Stats != null ? Stats.MaxHealth : 0f;

        protected override float RegenPerSecond => Stats != null ? Stats.HealthRegenPerSecond : 0f;

        protected override float RegenDelaySeconds => Stats != null ? Stats.HealthRegenDelay : 0f;

        protected override bool CanRegenerate => IsAlive;

        /// <summary>Applies damage. Returns how much health was actually lost.</summary>
        public float TakeDamage(DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0f)
            {
                return 0f;
            }

            float taken = Drain(damage.Amount);

            if (taken <= 0f)
            {
                return 0f;
            }

            Damaged?.Invoke(damage, taken);

            if (IsDepleted)
            {
                IsAlive = false;
                GameLog.Info(LogChannel.Combat, $"{name} died to {damage}.", this);
                Died?.Invoke(damage);
            }

            return taken;
        }

        /// <summary>Restores health. Does nothing to the dead; reviving is a separate decision.</summary>
        public float Heal(float amount)
        {
            return IsAlive ? Replenish(amount) : 0f;
        }

        /// <summary>Brings a dead character back at the given fraction of maximum health.</summary>
        public void Revive(float normalizedHealth = 1f)
        {
            IsAlive = true;
            SetCurrent(Max * Mathf.Clamp01(normalizedHealth));
        }

        /// <summary>
        /// Restores saved state. Sets the alive flag directly rather than inferring it, because a
        /// save taken at exactly zero health is ambiguous otherwise.
        /// </summary>
        public void RestoreState(float current, bool alive)
        {
            IsAlive = alive;
            SetCurrent(current);
        }
    }
}
