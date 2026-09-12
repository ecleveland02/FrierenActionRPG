namespace Frieren.Core.Magic
{
    /// <summary>
    /// Something in the world that magic can act on: a crate, a lock, a mechanism, a body of water.
    /// </summary>
    /// <remarks>
    /// Lives in Core for the same reason as <c>IInteractable</c>. Spells need to call it and world
    /// objects need to implement it, and those are separate assemblies that must not depend on each
    /// other. Putting the contract in the lowest layer lets both sides reach it.
    ///
    /// Distinct from <c>IInteractable</c> on purpose. Pressing a lever and setting it on fire are
    /// different verbs with different rules, and collapsing them would force every flammable crate
    /// to pretend it has an interaction prompt.
    /// </remarks>
    public interface IMagicReceiver
    {
        /// <summary>
        /// Offers a pulse to this receiver.
        /// </summary>
        /// <returns>
        /// True if the pulse did something. Callers use this for feedback - a spell that reports
        /// affecting nothing can say so, rather than leaving the player unsure whether they missed
        /// or whether the object simply does not care about that element.
        /// </returns>
        bool ReceiveMagic(in MagicPulse pulse);
    }
}
