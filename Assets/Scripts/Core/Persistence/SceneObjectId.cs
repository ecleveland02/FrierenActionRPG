using UnityEngine;

namespace Frieren.Core.Persistence
{
    /// <summary>
    /// A stable name for one object, so a save file can find it again next time.
    /// </summary>
    /// <remarks>
    /// The hard part of persistence is identity, not serialisation. The tempting answers are all
    /// wrong: a hierarchy path breaks the moment something is reparented or renamed, an instance id
    /// is different every run, and a sibling index changes when anyone inserts a prop. So the id is
    /// authored, and an editor check refuses duplicates and blanks - the two ways an authored id
    /// fails, both of which are silent otherwise.
    ///
    /// Runtime-spawned objects get theirs from whatever spawned them, through
    /// <see cref="Assign"/>. A spawner knows which of its spawns is which; nothing else does.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneObjectId : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Unique within the project. Persisted verbatim: changing it orphans existing saves.")]
        private string id;

        public string Id => id;

        public bool HasId => !string.IsNullOrWhiteSpace(id);

        /// <summary>
        /// Names an object created at runtime. Callers must derive something reproducible - a
        /// spawner's own id plus a spawn index, not a counter that depends on what happened first.
        /// </summary>
        public void Assign(string runtimeId)
        {
            id = runtimeId;
        }
    }
}
