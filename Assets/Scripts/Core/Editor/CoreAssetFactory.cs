using System.IO;
using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Core.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Core.EditorTools
{
    /// <summary>
    /// Creates and wires the ScriptableObject assets the bootstrapper depends on.
    /// </summary>
    /// <remarks>
    /// Existing assets are never overwritten, only filled in where a field is empty, so running
    /// this is safe after the assets have been edited by hand.
    /// </remarks>
    internal static class CoreAssetFactory
    {
        public static void EnsureAll()
        {
            GameSceneDefinition boot = EnsureSceneDefinition(
                ProjectPaths.BootSceneDefinition, "scene.boot", "Boot", SceneKind.Boot);

            GameSceneDefinition test = EnsureSceneDefinition(
                ProjectPaths.TestSceneDefinition, "scene.test", "TestScene", SceneKind.Gameplay);

            EnsureSceneCatalog(boot, test);
            EnsureLogSettings();
            EnsureInputReader();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static GameSceneDefinition EnsureSceneDefinition(string path, string id, string sceneName, SceneKind kind)
        {
            var definition = AssetDatabase.LoadAssetAtPath<GameSceneDefinition>(path);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<GameSceneDefinition>();
                CreateAsset(definition, path);
            }

            var serialized = new SerializedObject(definition);
            SetIfEmpty(serialized, "id", id);
            SetIfEmpty(serialized, "displayName", sceneName);
            SetIfEmpty(serialized, "sceneName", sceneName);
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        private static void EnsureSceneCatalog(params GameSceneDefinition[] definitions)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProjectPaths.SceneCatalog);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SceneCatalog>();
                CreateAsset(catalog, ProjectPaths.SceneCatalog);
            }

            var serialized = new SerializedObject(catalog);
            SetIfEmpty(serialized, "id", "scene.catalog");
            SerializedProperty scenes = serialized.FindProperty("scenes");

            foreach (GameSceneDefinition definition in definitions)
            {
                if (definition != null && !CatalogContains(scenes, definition))
                {
                    scenes.InsertArrayElementAtIndex(scenes.arraySize);
                    scenes.GetArrayElementAtIndex(scenes.arraySize - 1).objectReferenceValue = definition;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool CatalogContains(SerializedProperty scenes, Object definition)
        {
            for (int i = 0; i < scenes.arraySize; i++)
            {
                if (scenes.GetArrayElementAtIndex(i).objectReferenceValue == definition)
                {
                    return true;
                }
            }

            return false;
        }

        public static LogSettings EnsureLogSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LogSettings>(ProjectPaths.LogSettings);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LogSettings>();
                CreateAsset(settings, ProjectPaths.LogSettings);

                var serialized = new SerializedObject(settings);
                serialized.FindProperty("id").stringValue = "settings.log";
                serialized.FindProperty("showDebugOverlayOnStart").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return settings;
        }

        public static InputReader EnsureInputReader()
        {
            var reader = AssetDatabase.LoadAssetAtPath<InputReader>(ProjectPaths.InputReader);

            if (reader == null)
            {
                reader = ScriptableObject.CreateInstance<InputReader>();
                CreateAsset(reader, ProjectPaths.InputReader);
            }

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectPaths.InputActions);

            if (actions == null)
            {
                Debug.LogWarning($"[Setup] No input actions asset at {ProjectPaths.InputActions}; InputReader is unwired.");
                return reader;
            }

            var serialized = new SerializedObject(reader);
            SerializedProperty actionsProperty = serialized.FindProperty("actions");

            if (actionsProperty.objectReferenceValue == null)
            {
                actionsProperty.objectReferenceValue = actions;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return reader;
        }

        private static void CreateAsset(Object asset, string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[Setup] Created {path}");
        }

        private static void SetIfEmpty(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property != null && string.IsNullOrWhiteSpace(property.stringValue))
            {
                property.stringValue = value;
            }
        }
    }
}
