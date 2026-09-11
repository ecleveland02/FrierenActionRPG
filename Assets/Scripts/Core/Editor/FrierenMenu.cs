using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Frieren.Core.EditorTools
{
    /// <summary>Editor entry points for everyday project chores.</summary>
    internal static class FrierenMenu
    {
        [MenuItem("Frieren/Open Boot Scene _F7", priority = 0)]
        private static void OpenBootScene() => OpenScene(ProjectPaths.BootScene);

        [MenuItem("Frieren/Open Test Scene _F8", priority = 1)]
        private static void OpenTestScene() => OpenScene(ProjectPaths.TestScene);

        [MenuItem("Frieren/Setup/Create Missing Core Assets", priority = 20)]
        private static void CreateMissingAssets() => CoreAssetFactory.EnsureAll();

        [MenuItem("Frieren/Setup/Configure Build Settings", priority = 21)]
        private static void ConfigureBuildSettings() => CoreSceneGenerator.ConfigureBuildSettings();

        [MenuItem("Frieren/Setup/Regenerate Core Scenes", priority = 22)]
        private static void RegenerateCoreScenes()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Regenerate core scenes",
                "This overwrites Boot.unity and TestScene.unity with freshly generated versions. " +
                "Any manual edits to those two scenes are lost.\n\nContinue?",
                "Regenerate", "Cancel");

            if (confirmed)
            {
                CoreSceneGenerator.RegenerateAll();
            }
        }

        [MenuItem("Frieren/Saves/Open Save Folder", priority = 40)]
        private static void OpenSaveFolder()
        {
            string path = Path.Combine(Application.persistentDataPath, "Saves");
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Frieren/Saves/Delete All Saves", priority = 41)]
        private static void DeleteAllSaves()
        {
            string path = Path.Combine(Application.persistentDataPath, "Saves");

            if (!Directory.Exists(path))
            {
                Debug.Log("[Saves] Nothing to delete.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete all saves", $"Delete every save file in\n{path}?", "Delete", "Cancel"))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
            Debug.Log("[Saves] Save folder deleted.");
        }

        private static void OpenScene(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"[Setup] Scene not found: {path}. Try Frieren > Setup > Regenerate Core Scenes.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }
    }
}
