using System.Collections.Generic;
using Frieren.Data;
using UnityEngine;

namespace Frieren.Core.Scenes
{
    /// <summary>
    /// Every <see cref="GameSceneDefinition"/> the game can load, so a scene id read from a save
    /// file or a quest definition can be resolved back to an asset at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Frieren/Core/Scene Catalog", fileName = "SceneCatalog", order = 1)]
    public sealed class SceneCatalog : IdentifiableScriptableObject
    {
        [SerializeField] private List<GameSceneDefinition> scenes = new List<GameSceneDefinition>();

        public IReadOnlyList<GameSceneDefinition> Scenes => scenes;

        public bool TryGetById(string sceneId, out GameSceneDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                for (int i = 0; i < scenes.Count; i++)
                {
                    if (scenes[i] != null && scenes[i].Id == sceneId)
                    {
                        definition = scenes[i];
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }
    }
}
