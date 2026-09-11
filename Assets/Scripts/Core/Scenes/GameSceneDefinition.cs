using Frieren.Data;
using UnityEngine;

namespace Frieren.Core.Scenes
{
    /// <summary>
    /// Authored description of a loadable scene.
    /// </summary>
    /// <remarks>
    /// Gameplay code references this asset instead of a scene name or build index. Build indices
    /// reorder silently and break save files that stored one; the asset's <c>Id</c> is stable, so
    /// a save can record where the player was and still resolve it after the build list changes.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Core/Game Scene Definition", fileName = "Scene_", order = 0)]
    public sealed class GameSceneDefinition : IdentifiableScriptableObject
    {
        [Header("Scene")]
        [SerializeField]
        [Tooltip("Scene file name without extension. Must also be present in Build Settings.")]
        private string sceneName;

        [SerializeField] private SceneKind kind = SceneKind.Gameplay;

        public string SceneName => sceneName;

        public SceneKind Kind => kind;

        public bool IsValid => !string.IsNullOrWhiteSpace(sceneName);

        public override string ToString() => $"{Id} ({sceneName})";
    }
}
