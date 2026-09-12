namespace Frieren.Characters
{
    /// <summary>
    /// Something that stands between incoming damage and a character's health.
    /// </summary>
    /// <remarks>
    /// The seam barriers, armour, resistances and damage-reduction buffs all attach to.
    /// <see cref="CharacterHealth"/> runs every modifier on the character in order and applies
    /// whatever is left, so none of them has to know the others exist.
    ///
    /// Modifiers may consume their own resources - a barrier spends itself absorbing - which is why
    /// this returns the surviving amount rather than a multiplier.
    /// </remarks>
    public interface IDamageModifier
    {
        /// <summary>Lower runs first. Barriers sit in front of armour, so they use a lower number.</summary>
        int ModifierOrder { get; }

        /// <summary>
        /// Takes what it can from the incoming amount and returns the remainder.
        /// </summary>
        /// <param name="damage">The original hit, for modifiers that care about its type or source.</param>
        /// <param name="amount">What is left after earlier modifiers.</param>
        float ModifyIncomingDamage(in DamageInfo damage, float amount);
    }
}
