namespace Frieren.Core.Persistence
{
    /// <summary>
    /// A component with state worth remembering across a save and load.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>ISaveable</c>. That interface belongs to <c>Frieren.Save</c>, and a
    /// burning crate should not have to know that a save system exists any more than it knows what
    /// Fire is - the whole design rests on world objects staying ignorant of the systems acting on
    /// them. This lives in Core for the same reason as <c>IMagicReceiver</c>: both sides need the
    /// contract and neither may depend on the other.
    ///
    /// <see cref="PersistentObject"/> is the adapter that collects these and speaks to the save
    /// system on their behalf, so one GameObject produces one save entry no matter how many
    /// persistent components it carries.
    ///
    /// State is JSON of a plain <c>[Serializable]</c> class of value types and strings. Never an
    /// object reference: it cannot survive a reload, and the failure is silent.
    /// </remarks>
    public interface IPersistentState
    {
        /// <summary>
        /// Identifies this component's slice of its object's state, e.g. "burn" or "basin".
        /// Persisted verbatim, so changing it orphans existing data.
        /// </summary>
        string StateKey { get; }

        string CaptureState();

        /// <summary>Called only when a loaded save actually holds a slice under <see cref="StateKey"/>.</summary>
        void RestoreState(string json);
    }
}
