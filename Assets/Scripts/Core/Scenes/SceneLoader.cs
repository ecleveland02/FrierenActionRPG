using System;
using System.Collections;
using System.Collections.Generic;
using Frieren.Core.Debugging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frieren.Core.Scenes
{
    /// <summary>
    /// Loads and unloads gameplay scenes additively on top of the persistent Boot scene.
    /// </summary>
    /// <remarks>
    /// Everything is additive, including the "main" gameplay scene. A single-mode load would tear
    /// down the bootstrap object and every service with it, and the town/forest/ruins/dungeon flow
    /// in Milestone 7 needs to be able to hold two scenes resident during a transition anyway.
    /// The cost is that the loader must track which gameplay scene is current and unload it
    /// explicitly, which is what <see cref="ActiveGameplayScene"/> is for.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        private readonly List<GameSceneDefinition> loadedScenes = new List<GameSceneDefinition>();

        public bool IsBusy { get; private set; }

        /// <summary>The gameplay scene the player is currently in, if any.</summary>
        public GameSceneDefinition ActiveGameplayScene { get; private set; }

        public IReadOnlyList<GameSceneDefinition> LoadedScenes => loadedScenes;

        public event Action<GameSceneDefinition> LoadStarted;

        /// <summary>Reports 0..1 while a load is in flight. Useful for a loading screen.</summary>
        public event Action<float> LoadProgressChanged;

        public event Action<GameSceneDefinition> LoadCompleted;

        public event Action<GameSceneDefinition> SceneUnloaded;

        /// <summary>
        /// Loads <paramref name="definition"/> and unloads the previous gameplay scene once the
        /// new one is resident, so systems are never left with no world around them.
        /// </summary>
        public Coroutine TransitionToGameplayScene(GameSceneDefinition definition, Action onCompleted = null)
        {
            return StartCoroutine(TransitionRoutine(definition, onCompleted));
        }

        public Coroutine LoadAdditive(GameSceneDefinition definition, Action onCompleted = null)
        {
            return StartCoroutine(LoadRoutine(definition, setActive: false, onCompleted));
        }

        public Coroutine Unload(GameSceneDefinition definition, Action onCompleted = null)
        {
            return StartCoroutine(UnloadRoutine(definition, onCompleted));
        }

        public bool IsLoaded(GameSceneDefinition definition)
        {
            return definition != null && definition.IsValid && SceneManager.GetSceneByName(definition.SceneName).isLoaded;
        }

        private IEnumerator TransitionRoutine(GameSceneDefinition definition, Action onCompleted)
        {
            GameSceneDefinition previous = ActiveGameplayScene;

            yield return LoadRoutine(definition, setActive: true, onCompleted: null);

            if (previous != null && previous != definition)
            {
                yield return UnloadRoutine(previous, onCompleted: null);
            }

            if (IsLoaded(definition))
            {
                ActiveGameplayScene = definition;
            }

            onCompleted?.Invoke();
        }

        private IEnumerator LoadRoutine(GameSceneDefinition definition, bool setActive, Action onCompleted)
        {
            if (!ValidateRequest(definition))
            {
                onCompleted?.Invoke();
                yield break;
            }

            if (IsLoaded(definition))
            {
                GameLog.Warn(LogChannel.Scenes, $"Scene '{definition}' is already loaded; skipping load.");
                onCompleted?.Invoke();
                yield break;
            }

            IsBusy = true;
            LoadStarted?.Invoke(definition);
            GameLog.Info(LogChannel.Scenes, $"Loading scene {definition}.");

            AsyncOperation operation = SceneManager.LoadSceneAsync(definition.SceneName, LoadSceneMode.Additive);

            if (operation == null)
            {
                GameLog.Error(LogChannel.Scenes,
                    $"Scene '{definition.SceneName}' could not be loaded. Is it in Build Settings?");
                IsBusy = false;
                onCompleted?.Invoke();
                yield break;
            }

            while (!operation.isDone)
            {
                LoadProgressChanged?.Invoke(operation.progress);
                yield return null;
            }

            LoadProgressChanged?.Invoke(1f);

            Scene scene = SceneManager.GetSceneByName(definition.SceneName);

            if (setActive && scene.isLoaded)
            {
                // The active scene supplies lighting and skybox, and receives newly instantiated objects.
                SceneManager.SetActiveScene(scene);
            }

            if (!loadedScenes.Contains(definition))
            {
                loadedScenes.Add(definition);
            }

            IsBusy = false;
            LoadCompleted?.Invoke(definition);
            onCompleted?.Invoke();
        }

        private IEnumerator UnloadRoutine(GameSceneDefinition definition, Action onCompleted)
        {
            if (!ValidateRequest(definition) || !IsLoaded(definition))
            {
                onCompleted?.Invoke();
                yield break;
            }

            GameLog.Info(LogChannel.Scenes, $"Unloading scene {definition}.");

            AsyncOperation operation = SceneManager.UnloadSceneAsync(definition.SceneName);

            while (operation != null && !operation.isDone)
            {
                yield return null;
            }

            loadedScenes.Remove(definition);

            if (ActiveGameplayScene == definition)
            {
                ActiveGameplayScene = null;
            }

            SceneUnloaded?.Invoke(definition);
            onCompleted?.Invoke();
        }

        private static bool ValidateRequest(GameSceneDefinition definition)
        {
            if (definition == null)
            {
                GameLog.Error(LogChannel.Scenes, "Scene load requested with a null definition.");
                return false;
            }

            if (!definition.IsValid)
            {
                GameLog.Error(LogChannel.Scenes, $"Scene definition '{definition.Id}' has no scene name assigned.");
                return false;
            }

            return true;
        }
    }
}
