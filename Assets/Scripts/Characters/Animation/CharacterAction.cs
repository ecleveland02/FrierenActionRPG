namespace Frieren.Characters.Animation
{
    /// <summary>
    /// One-shot animated actions. Continuous state (speed, grounded) is not in here - that is set
    /// as values, not triggered.
    /// </summary>
    public enum CharacterAction
    {
        Jump = 0,
        Land = 1,
        Dodge = 2,
        Attack = 3,
        CastStart = 4,
        CastRelease = 5,
        Hit = 6,
        Death = 7
    }
}
