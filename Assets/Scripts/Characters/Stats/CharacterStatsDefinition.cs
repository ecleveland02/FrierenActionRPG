using Frieren.Data;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// The authored numbers for one kind of character: the player, a wolf, a boss.
    /// </summary>
    /// <remarks>
    /// An asset per archetype rather than values typed onto each prefab. Milestone 5 wants several
    /// enemies sharing one template, and progression later needs a base for modifiers to apply to -
    /// a spell that grants "+20% maximum mana" has nothing to multiply if the only number lives on
    /// an instance. It also puts every tuning value for a character in one place a designer can
    /// open without touching a prefab.
    ///
    /// These are base values. Nothing here is the current state of a live character; that belongs
    /// to <see cref="CharacterHealth"/> and <see cref="CharacterMana"/>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Characters/Character Stats", fileName = "Stats_", order = 0)]
    public sealed class CharacterStatsDefinition : IdentifiableScriptableObject
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [SerializeField]
        [Tooltip("Health restored per second. Zero means no passive regeneration, which is the usual choice.")]
        private float healthRegenPerSecond;

        [SerializeField]
        [Tooltip("Seconds after taking damage before health regeneration resumes.")]
        private float healthRegenDelay = 5f;

        [Header("Mana")]
        [SerializeField] private float maxMana = 100f;

        [SerializeField] private float manaRegenPerSecond = 4f;

        [SerializeField]
        [Tooltip("Seconds after spending mana before regeneration resumes. This is the main lever on casting pace.")]
        private float manaRegenDelay = 1.5f;

        public float MaxHealth => Mathf.Max(0f, maxHealth);

        public float HealthRegenPerSecond => Mathf.Max(0f, healthRegenPerSecond);

        public float HealthRegenDelay => Mathf.Max(0f, healthRegenDelay);

        public float MaxMana => Mathf.Max(0f, maxMana);

        public float ManaRegenPerSecond => Mathf.Max(0f, manaRegenPerSecond);

        public float ManaRegenDelay => Mathf.Max(0f, manaRegenDelay);
    }
}
