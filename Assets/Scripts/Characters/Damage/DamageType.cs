namespace Frieren.Characters
{
    /// <summary>
    /// What kind of harm a hit represents.
    /// </summary>
    /// <remarks>
    /// Nothing reads this yet - there are no resistances until enemies exist. It is here because
    /// it belongs in <see cref="DamageInfo"/>, and adding a field to that struct later would mean
    /// revisiting every call site that deals damage. The elemental values line up with the spells
    /// planned for Milestone 4 so a fire spell has somewhere to say it is fire.
    /// </remarks>
    public enum DamageType
    {
        Physical = 0,
        Fire = 1,
        Ice = 2,
        Arcane = 3,

        /// <summary>Bypasses resistances by convention. For falling, drowning, scripted deaths.</summary>
        True = 4
    }
}
