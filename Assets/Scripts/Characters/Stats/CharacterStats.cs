using System;
using Frieren.Core.Debugging;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// A character's resolved stats: the authored base, plus whatever modifies it.
    /// </summary>
    /// <remarks>
    /// Everything reads its numbers through this component rather than from the definition asset
    /// directly. Nothing modifies anything yet, so today it is a passthrough - but it is the seam
    /// that equipment, buffs and progression plug into later, and putting it in now means those do
    /// not require touching health, mana or any spell that reads a stat.
    ///
    /// <see cref="Changed"/> exists so resources can resync when a maximum moves under them.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterStats : MonoBehaviour
    {
        [SerializeField] private CharacterStatsDefinition definition;

        /// <summary>Raised when any resolved stat may have changed.</summary>
        public event Action Changed;

        public CharacterStatsDefinition Definition => definition;

        public bool HasDefinition => definition != null;

        public float MaxHealth => definition != null ? definition.MaxHealth : 0f;

        public float HealthRegenPerSecond => definition != null ? definition.HealthRegenPerSecond : 0f;

        public float HealthRegenDelay => definition != null ? definition.HealthRegenDelay : 0f;

        public float MaxMana => definition != null ? definition.MaxMana : 0f;

        public float ManaRegenPerSecond => definition != null ? definition.ManaRegenPerSecond : 0f;

        public float ManaRegenDelay => definition != null ? definition.ManaRegenDelay : 0f;

        private void Awake()
        {
            if (definition == null)
            {
                GameLog.Error(LogChannel.Player,
                    $"{name}: CharacterStats has no definition assigned, so every stat reads zero " +
                    "and this character cannot be harmed, healed or cast anything.", this);
            }
        }

        /// <summary>
        /// Swaps the archetype at runtime. Mostly for tests and for enemies built from a spawner
        /// rather than a bespoke prefab.
        /// </summary>
        public void SetDefinition(CharacterStatsDefinition newDefinition)
        {
            definition = newDefinition;
            Changed?.Invoke();
        }

        /// <summary>Announces that modifiers have changed, once there are any.</summary>
        public void NotifyChanged() => Changed?.Invoke();
    }
}
