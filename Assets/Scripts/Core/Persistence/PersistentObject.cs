using System;
using System.Collections.Generic;
using Frieren.Core.Debugging;
using Frieren.Core.Services;
using Frieren.Save;
using Frieren.Save.Serialization;
using UnityEngine;

namespace Frieren.Core.Persistence
{
    /// <summary>
    /// Saves and restores everything on one GameObject that has state worth keeping.
    /// </summary>
    /// <remarks>
    /// The single adapter between <see cref="IPersistentState"/> and the save system. Gameplay
    /// components implement the Core interface and know nothing about save files; this speaks
    /// <c>ISaveable</c> on their behalf.
    ///
    /// One entry per object rather than per component. A door that is locked, burnable and
    /// interactive is one thing in the world and should be one thing in the file, and adding a
    /// fourth behaviour to it should not add a fourth key.
    ///
    /// Registration happens in <c>Start</c>, and a spawned object arriving after a load is
    /// already handled: <c>SaveService</c> restores late registrations itself. That is what makes
    /// enemies spawned at runtime work at all.
    /// </remarks>
    [RequireComponent(typeof(SceneObjectId))]
    [DisallowMultipleComponent]
    public sealed class PersistentObject : MonoBehaviour, ISaveable
    {
        [Serializable]
        private sealed class Slice
        {
            public string key;
            public string json;
        }

        [Serializable]
        private sealed class ObjectState
        {
            public List<Slice> slices = new List<Slice>();
        }

        private SceneObjectId objectId;
        private IPersistentState[] parts;
        private SaveService saveService;

        public string SaveId => objectId != null ? objectId.Id : string.Empty;

        private void Awake()
        {
            objectId = GetComponent<SceneObjectId>();
            parts = GetComponentsInChildren<IPersistentState>(true);
        }

        private void Start()
        {
            if (!objectId.HasId)
            {
                GameLog.Error(LogChannel.Save,
                    $"{name}: PersistentObject has no id, so its state cannot be saved or found again. " +
                    "Give its SceneObjectId one, or have whatever spawned it assign one.", this);
                return;
            }

            if (parts.Length == 0)
            {
                return;
            }

            if (!ServiceLocator.TryGet(out saveService))
            {
                GameLog.Warn(LogChannel.Save,
                    $"{name}: no SaveService, so '{SaveId}' will not persist. Is the Boot scene loaded?", this);
                return;
            }

            saveService.Register(this);
        }

        private void OnDestroy()
        {
            if (saveService != null)
            {
                saveService.Unregister(this);
            }
        }

        public SaveEntry Capture()
        {
            var state = new ObjectState();

            for (int i = 0; i < parts.Length; i++)
            {
                string json = parts[i].CaptureState();

                if (!string.IsNullOrEmpty(json))
                {
                    state.slices.Add(new Slice { key = parts[i].StateKey, json = json });
                }
            }

            return SaveEntry.Create(SaveId, state);
        }

        public void Restore(SaveEntry entry)
        {
            if (!entry.TryRead(out ObjectState state) || state.slices == null)
            {
                return;
            }

            for (int i = 0; i < state.slices.Count; i++)
            {
                Slice slice = state.slices[i];
                IPersistentState part = FindPart(slice.key);

                if (part == null)
                {
                    // An old save naming a component that has since been removed. Not an error:
                    // dropping a behaviour should not invalidate everyone's saves.
                    continue;
                }

                part.RestoreState(slice.json);
            }
        }

        private IPersistentState FindPart(string key)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StateKey == key)
                {
                    return parts[i];
                }
            }

            return null;
        }
    }
}
