namespace Frieren.Magic
{
    /// <summary>
    /// Whether a spell resolves once or keeps working while held.
    /// </summary>
    public enum SpellCastMode
    {
        /// <summary>Cast, resolve, done. Bolts, bursts, blinks.</summary>
        Instant = 0,

        /// <summary>
        /// Keeps running while the cast input is held, re-applying its effects on a tick and
        /// draining mana per second. Levitation, beams, sustained shields.
        /// </summary>
        Channelled = 1
    }
}
