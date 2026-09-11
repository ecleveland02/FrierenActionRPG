using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Save.Serialization
{
    /// <summary>
    /// Root object written to disk for a single save slot.
    /// </summary>
    [Serializable]
    public sealed class SaveGameData
    {
        /// <summary>Bump whenever the file layout changes, and add a step to <see cref="SaveMigration"/>.</summary>
        public const int CurrentVersion = 1;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private string createdUtc;
        [SerializeField] private string savedUtc;
        [SerializeField] private string sceneId;
        [SerializeField] private double playTimeSeconds;

        // JsonUtility cannot serialise a Dictionary, so entries are stored as a list and looked
        // up linearly. Save files hold tens of entries, not thousands.
        [SerializeField] private List<SaveEntry> entries = new List<SaveEntry>();

        public SaveGameData()
        {
            createdUtc = DateTime.UtcNow.ToString("o");
            savedUtc = createdUtc;
        }

        public int Version
        {
            get => version;
            internal set => version = value;
        }

        public string CreatedUtc => createdUtc;

        public string SavedUtc => savedUtc;

        /// <summary>Id of the <c>GameSceneDefinition</c> the player was in when the file was written.</summary>
        public string SceneId
        {
            get => sceneId;
            set => sceneId = value;
        }

        public double PlayTimeSeconds
        {
            get => playTimeSeconds;
            set => playTimeSeconds = value;
        }

        public IReadOnlyList<SaveEntry> Entries => entries;

        public void MarkSaved()
        {
            savedUtc = DateTime.UtcNow.ToString("o");
        }

        /// <summary>Adds the entry, replacing any existing entry with the same key.</summary>
        public void Put(SaveEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Key == entry.Key)
                {
                    entries[i] = entry;
                    return;
                }
            }

            entries.Add(entry);
        }

        public bool TryGet(string key, out SaveEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Key == key)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public bool Remove(string key)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Key == key)
                {
                    entries.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }
}
