using System.Collections.Generic;

namespace Frieren.Save.Storage
{
    /// <summary>
    /// Non-persistent storage for tests and for editor sessions that should not write to disk.
    /// </summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> payloads = new Dictionary<string, string>();

        public bool Exists(string slotId) => payloads.ContainsKey(slotId);

        public void Write(string slotId, string payload) => payloads[slotId] = payload;

        public bool TryRead(string slotId, out string payload) => payloads.TryGetValue(slotId, out payload);

        public bool Delete(string slotId) => payloads.Remove(slotId);

        public IReadOnlyList<string> ListSlots()
        {
            var slots = new List<string>(payloads.Keys);
            slots.Sort();
            return slots;
        }
    }
}
