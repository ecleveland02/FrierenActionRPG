using Frieren.Save.Serialization;

namespace Frieren.Save
{
    /// <summary>
    /// Implemented by anything that contributes state to a save file.
    /// </summary>
    /// <remarks>
    /// Implementations register themselves with <see cref="SaveService"/> and are responsible for
    /// their own state object. Keep state objects as plain <c>[Serializable]</c> classes of value
    /// types and strings - never store direct object references, which cannot survive a reload.
    /// </remarks>
    public interface ISaveable
    {
        /// <summary>
        /// Stable, unique key for this saveable. Persisted verbatim, so treat it as content:
        /// changing it orphans existing data.
        /// </summary>
        string SaveId { get; }

        SaveEntry Capture();

        /// <summary>Called only when the loaded file actually contains an entry for <see cref="SaveId"/>.</summary>
        void Restore(SaveEntry entry);
    }
}
