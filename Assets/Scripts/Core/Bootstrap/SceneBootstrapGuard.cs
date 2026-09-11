using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frieren.Core.Bootstrap
{
    /// <summary>
    /// Lets any scene be entered directly in the editor by pulling the Boot scene in behind it.
    /// </summary>
    /// <remarks>
    /// Without this, pressing Play in a gameplay scene gives a world with no services, and the
    /// only way to test anything is to start from Boot and walk there. The auto-load is editor
    /// only: a build always starts at Boot, so the guard costs nothing at runtime.
    ///
    /// <see cref="BootedFromAnotherScene"/> tells <see cref="Bootstrapper"/> not to load its first
    /// scene in that case, so the scene being tested is not immediately replaced.
    /// </remarks>
    public static class SceneBootstrapGuard
    {
        /// <summary>Scene file name of the Boot scene. It must be first in Build Settings.</summary>
        public const string BootSceneName = "Boot";

        /// <summary>True when play mode began in a scene other than Boot.</summary>
        public static bool BootedFromAnotherScene { get; private set; }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            BootedFromAnotherScene = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootSceneLoaded()
        {
            if (Bootstrapper.IsInitialized)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.name == BootSceneName)
            {
                return;
            }

            if (SceneManager.GetSceneByName(BootSceneName).isLoaded)
            {
                return;
            }

            BootedFromAnotherScene = true;
            Debug.Log($"[Core] Play mode started in '{activeScene.name}'; loading '{BootSceneName}' for services.");
            SceneManager.LoadScene(BootSceneName, LoadSceneMode.Additive);
        }
#endif
    }
}
