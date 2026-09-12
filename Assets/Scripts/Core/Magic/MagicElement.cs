namespace Frieren.Core.Magic
{
    /// <summary>
    /// The kinds of magical influence a spell can exert on the world.
    /// </summary>
    /// <remarks>
    /// World objects react to these, never to a spell's identity. A crate burns because it received
    /// enough <see cref="Heat"/>, not because it was hit by "Fire" - so a later explosion, a lava
    /// pool or a burning arrow lights the same crate with no change to the crate. That is the whole
    /// mechanism behind the project's central promise that a problem should have more than one
    /// solution.
    ///
    /// Keep this list short. Every value is a vocabulary word every future puzzle object has to
    /// understand, and two elements that behave identically are one element with two names.
    /// </remarks>
    public enum MagicElement
    {
        /// <summary>Raw magical force with no elemental character. The basic projectile.</summary>
        Arcane = 0,

        /// <summary>Ignites, melts, dries, cooks.</summary>
        Heat = 1,

        /// <summary>Freezes, extinguishes, makes brittle, solidifies liquid.</summary>
        Cold = 2,

        /// <summary>Pushes, lifts, holds. Levitation and knockback.</summary>
        Force = 3,

        /// <summary>Creates or moves water. Fills, floats, conducts.</summary>
        Water = 4,

        /// <summary>Mends what is broken. Repair magic.</summary>
        Restoration = 5,

        /// <summary>Undoes bindings: locks, seals, wards.</summary>
        Unbinding = 6
    }
}
