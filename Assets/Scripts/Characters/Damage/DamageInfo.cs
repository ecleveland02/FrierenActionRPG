using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// One instance of harm: how much, of what kind, from whom.
    /// </summary>
    /// <remarks>
    /// A struct rather than a bare float so the signature of <see cref="CharacterHealth.TakeDamage"/>
    /// does not have to change when resistances, critical hits or knockback arrive. Changing that
    /// signature later would touch every attack, spell and hazard in the game.
    /// </remarks>
    public readonly struct DamageInfo
    {
        public DamageInfo(float amount, DamageType type = DamageType.Physical, GameObject source = null)
        {
            Amount = Mathf.Max(0f, amount);
            Type = type;
            Source = source;
        }

        public float Amount { get; }

        public DamageType Type { get; }

        /// <summary>Who caused it, if anyone. Null for environmental damage.</summary>
        public GameObject Source { get; }

        public override string ToString() =>
            $"{Amount:0.#} {Type}" + (Source != null ? $" from {Source.name}" : string.Empty);
    }
}
