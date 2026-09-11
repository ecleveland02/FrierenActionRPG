using System;
using UnityEngine;

namespace Frieren.Save.Serialization
{
    /// <summary>
    /// One system's persisted state, stored as a JSON blob under a stable key.
    /// </summary>
    /// <remarks>
    /// The save file deliberately does not know the shape of any system's state. Each
    /// <c>ISaveable</c> serialises its own plain state object, which means a new system can
    /// start persisting data without touching the save file format, and an old save missing that
    /// key simply leaves the system at its defaults.
    /// </remarks>
    [Serializable]
    public sealed class SaveEntry
    {
        [SerializeField] private string key;
        [SerializeField] private string typeName;
        [SerializeField] private string json;

        /// <summary>Required by <see cref="JsonUtility"/>; use <see cref="Create{TState}"/> instead.</summary>
        public SaveEntry()
        {
        }

        public string Key => key;

        /// <summary>
        /// Name of the type the blob was written from. Diagnostics only - it is never used to
        /// resolve a type, so renaming or moving a state class does not invalidate saves.
        /// </summary>
        public string TypeName => typeName;

        public string Json => json;

        public static SaveEntry Create<TState>(string key, TState state) where TState : class
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Save entry key must not be empty.", nameof(key));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new SaveEntry
            {
                key = key,
                typeName = typeof(TState).FullName,
                json = JsonUtility.ToJson(state)
            };
        }

        public bool TryRead<TState>(out TState state) where TState : class
        {
            state = null;

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                state = JsonUtility.FromJson<TState>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Save entry '{key}' could not be read as {typeof(TState).Name}: {exception.Message}");
                return false;
            }

            return state != null;
        }
    }
}
