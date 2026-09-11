using System.Collections.Generic;

namespace Frieren.Save.Storage
{
    /// <summary>
    /// Where save payloads physically live.
    /// </summary>
    /// <remarks>
    /// This is the seam that keeps the save system testable: edit-mode tests run against
    /// <see cref="InMemorySaveStorage"/> and never touch the disk. It is also the hook for
    /// platform storage or cloud saves later, with no change to <c>SaveService</c>.
    /// </remarks>
    public interface ISaveStorage
    {
        bool Exists(string slotId);

        void Write(string slotId, string payload);

        bool TryRead(string slotId, out string payload);

        bool Delete(string slotId);

        IReadOnlyList<string> ListSlots();
    }
}
