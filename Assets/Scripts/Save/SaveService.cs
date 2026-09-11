using System;
using System.Collections.Generic;
using Frieren.Save.Serialization;
using Frieren.Save.Storage;
using UnityEngine;

namespace Frieren.Save
{
    /// <summary>
    /// Owns the save file in memory and brokers between it and the registered <see cref="ISaveable"/>s.
    /// </summary>
    /// <remarks>
    /// Loading a save is deliberately split from applying it. A load reads the file into
    /// <see cref="Current"/> and restores everything already registered; anything that registers
    /// later - a player spawned by a scene that is still streaming in, an enemy created by a
    /// spawner - is restored at registration time. Without this, load order would decide whether
    /// state survived a reload, which is exactly the kind of bug that is invisible until the
    /// dungeon in Milestone 7.
    /// </remarks>
    public sealed class SaveService
    {
        public const string DefaultSlotId = "slot_0";

        private readonly ISaveStorage storage;
        private readonly Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();

        public SaveService(ISaveStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>The save file currently in memory, or <c>null</c> if none has been loaded or started.</summary>
        public SaveGameData Current { get; private set; }

        public event Action<string> SaveWritten;

        public event Action<string> SaveLoaded;

        public event Action<string> SaveDeleted;

        public IReadOnlyCollection<string> RegisteredIds => saveables.Keys;

        /// <summary>Starts a fresh, unsaved file. Does not touch storage.</summary>
        public SaveGameData NewGame()
        {
            Current = new SaveGameData();
            return Current;
        }

        public void Register(ISaveable saveable)
        {
            if (saveable == null)
            {
                throw new ArgumentNullException(nameof(saveable));
            }

            string id = saveable.SaveId;

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"{saveable.GetType().Name} has an empty SaveId.", nameof(saveable));
            }

            if (saveables.TryGetValue(id, out ISaveable existing))
            {
                if (ReferenceEquals(existing, saveable))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Save id '{id}' is already registered by {existing.GetType().Name}. Save ids must be unique.");
            }

            saveables.Add(id, saveable);

            // A file may already be loaded; catch this saveable up rather than leaving it stale.
            if (Current != null && Current.TryGet(id, out SaveEntry entry))
            {
                saveable.Restore(entry);
            }
        }

        public bool Unregister(ISaveable saveable)
        {
            if (saveable == null)
            {
                return false;
            }

            string id = saveable.SaveId;

            if (!saveables.TryGetValue(id, out ISaveable existing) || !ReferenceEquals(existing, saveable))
            {
                return false;
            }

            return saveables.Remove(id);
        }

        public bool HasSave(string slotId) => storage.Exists(slotId);

        public IReadOnlyList<string> ListSlots() => storage.ListSlots();

        /// <summary>Captures every registered saveable and writes the slot to storage.</summary>
        public bool Save(string slotId = DefaultSlotId)
        {
            Current ??= new SaveGameData();

            foreach (KeyValuePair<string, ISaveable> pair in saveables)
            {
                SaveEntry entry = pair.Value.Capture();

                if (entry == null)
                {
                    continue;
                }

                if (entry.Key != pair.Key)
                {
                    Debug.LogError(
                        $"Saveable '{pair.Key}' produced an entry keyed '{entry.Key}'. Entries must use SaveId.");
                    continue;
                }

                Current.Put(entry);
            }

            Current.MarkSaved();

            try
            {
                storage.Write(slotId, JsonUtility.ToJson(Current, prettyPrint: true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to write save slot '{slotId}': {exception.Message}");
                return false;
            }

            SaveWritten?.Invoke(slotId);
            return true;
        }

        /// <summary>Reads the slot and restores every registered saveable that has an entry in it.</summary>
        public bool Load(string slotId = DefaultSlotId)
        {
            if (!storage.TryRead(slotId, out string payload) || string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            SaveGameData data;

            try
            {
                data = JsonUtility.FromJson<SaveGameData>(payload);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Save slot '{slotId}' is not valid JSON: {exception.Message}");
                return false;
            }

            if (!SaveMigration.TryMigrate(data, out string failureReason))
            {
                Debug.LogError($"Save slot '{slotId}' could not be loaded: {failureReason}");
                return false;
            }

            Current = data;
            RestoreAll();
            SaveLoaded?.Invoke(slotId);
            return true;
        }

        public bool Delete(string slotId = DefaultSlotId)
        {
            if (!storage.Delete(slotId))
            {
                return false;
            }

            SaveDeleted?.Invoke(slotId);
            return true;
        }

        /// <summary>Re-applies <see cref="Current"/> to everything registered right now.</summary>
        public void RestoreAll()
        {
            if (Current == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ISaveable> pair in saveables)
            {
                if (Current.TryGet(pair.Key, out SaveEntry entry))
                {
                    pair.Value.Restore(entry);
                }
            }
        }
    }
}
