using System;
using Frieren.Core.Debugging;
using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// A ward that absorbs damage until it is spent or lapses.
    /// </summary>
    /// <remarks>
    /// Defensive magic in this setting is a barrier held up by concentration, so this is granted by
    /// a Warding pulse and fades shortly after the pulses stop. A channelled barrier spell refreshes
    /// it every tick; let go and it drops.
    ///
    /// It absorbs through <see cref="IDamageModifier"/> rather than by health checking for a barrier.
    /// Health knows nothing about wards, armour or resistances - it applies what reaches it - which
    /// is what lets all three exist without any of them knowing about the others.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterBarrier : MonoBehaviour, IMagicReceiver, IDamageModifier
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("Warding that must arrive in one pulse to raise the barrier at all.")]
        private float wardThreshold = 5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Damage the barrier can absorb at full strength.")]
        private float capacity = 60f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the barrier survives after the last pulse. A channel refreshes this each tick.")]
        private float lapseDelay = 0.35f;

        [SerializeField]
        [Tooltip("Damage types the barrier cannot stop. True damage ignores wards by convention.")]
        private DamageType[] ignoredTypes = { DamageType.True };

        private float remaining;
        private float lapseAt;

        public event Action<float> Raised;

        public event Action<float> Absorbed;

        public event Action Broke;

        public event Action Lapsed;

        public bool IsUp => remaining > 0f;

        public float Remaining => remaining;

        public float Normalized => capacity <= 0f ? 0f : Mathf.Clamp01(remaining / capacity);

        /// <summary>In front of armour: a ward stops the hit before anything else gets a say.</summary>
        public int ModifierOrder => 0;

        private void Update()
        {
            if (remaining <= 0f || Time.time < lapseAt)
            {
                return;
            }

            remaining = 0f;
            GameLog.Info(LogChannel.Combat, $"{name}'s barrier lapsed.", this);
            Lapsed?.Invoke();
        }

        public bool ReceiveMagic(in MagicPulse pulse)
        {
            if (pulse.Element != MagicElement.Warding || pulse.Magnitude < wardThreshold)
            {
                return false;
            }

            bool wasDown = remaining <= 0f;

            // Refreshed to full rather than accumulated: a barrier is held, not stockpiled, and
            // stacking would make holding the spell in a corner the correct opening move.
            remaining = capacity;
            lapseAt = Time.time + lapseDelay;

            if (wasDown)
            {
                GameLog.Info(LogChannel.Combat, $"{name} raised a barrier.", this);
                Raised?.Invoke(remaining);
            }

            return true;
        }

        public float ModifyIncomingDamage(in DamageInfo damage, float amount)
        {
            if (remaining <= 0f || amount <= 0f || IsIgnored(damage.Type))
            {
                return amount;
            }

            float absorbed = Mathf.Min(remaining, amount);
            remaining -= absorbed;
            Absorbed?.Invoke(absorbed);

            if (remaining <= 0f)
            {
                GameLog.Info(LogChannel.Combat, $"{name}'s barrier broke.", this);
                Broke?.Invoke();
            }

            return amount - absorbed;
        }

        /// <summary>Drops the barrier immediately.</summary>
        public void Dispel()
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining = 0f;
            Lapsed?.Invoke();
        }

        private bool IsIgnored(DamageType type)
        {
            for (int i = 0; i < ignoredTypes.Length; i++)
            {
                if (ignoredTypes[i] == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
