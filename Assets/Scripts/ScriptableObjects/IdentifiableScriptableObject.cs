using UnityEngine;

namespace Frieren.Data
{
    /// <summary>
    /// Base class for authored content assets (spells, items, enemies, quests, scenes...).
    /// </summary>
    /// <remarks>
    /// <see cref="Id"/> is the contract between authored content and persisted data. Save files,
    /// quest conditions and spell unlocks all reference content by this string rather than by
    /// asset reference, so an asset can be renamed or moved without invalidating existing saves.
    /// Changing an <see cref="Id"/> after content ships is a breaking change.
    /// </remarks>
    public abstract class IdentifiableScriptableObject : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable identifier written into save files. Defaults to the asset name; do not change once used.")]
        private string id;

        [SerializeField] private string displayName;

        [SerializeField]
        [TextArea(2, 6)]
        private string description;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public string Description => description;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = name;
            }
        }
#endif
    }
}
