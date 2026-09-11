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
    ///
    /// There are three cases, and the third was learned the hard way:
    /// <list type="bullet">
    /// <item>Started in Boot - nothing to do.</item>
    /// <item>Started in a saved scene - load Boot additively and leave that scene alone, because
    /// the whole point is to test it.</item>
    /// <item>Started in an unsaved scene - which is what Unity opens on a fresh clone - load Boot
    /// in single mode and let it boot normally. Treating this like the second case gave running
    /// services, an empty world, and no error to explain it.</item>
    /// </list>
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

            // An unsaved scene has no asset path. Unity opens one of those when a project is first
            // cloned, and it has nothing in it worth protecting - so boot properly over the top of
            // it. Loading additively there produced the worst possible result: services running,
            // no world, no error, and a Game view showing nothing but the skybox.
            bool startedFromASavedScene = !string.IsNullOrEmpty(activeScene.path);

            if (startedFromASavedScene)
            {
                BootedFromAnotherScene = true;
                Debug.Log(
                    $"[Core] Play mode started in '{activeScene.name}'; loading '{BootSceneName}' additively " +
                    "for services, and leaving this scene in place.");
                SceneManager.LoadScene(BootSceneName, LoadSceneMode.Additive);
                return;
            }

            BootedFromAnotherScene = false;
            Debug.Log(
                $"[Core] Play mode started in an unsaved scene; booting '{BootSceneName}' normally. " +
                $"Open {BootSceneName} or a gameplay scene to skip this step.");
            SceneManager.LoadScene(BootSceneName, LoadSceneMode.Single);
        }
#endif
    }
}
