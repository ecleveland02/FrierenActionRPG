using System.Collections.Generic;
using System.IO;
using Frieren.Core.Bootstrap;
using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Core.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frieren.Core.EditorTools
{
    /// <summary>
    /// Rebuilds the Boot and Test scenes from code.
    /// </summary>
    /// <remarks>
    /// Scenes are checked in, so this is not needed day to day. It exists because a scene file is
    /// the one asset that cannot be reviewed in a diff, and a broken one is otherwise unrecoverable
    /// without redoing the wiring by hand. It also documents, in code, exactly what the Boot scene
    /// is supposed to contain.
    /// </remarks>
    internal static class CoreSceneGenerator
    {
        public static void RegenerateAll()
        {
            CoreAssetFactory.EnsureAll();
            Directory.CreateDirectory(ProjectPaths.ScenesFolder);

            BuildBootScene();
            BuildTestScene();

            AssetDatabase.Refresh();
            ConfigureBuildSettings();

            EditorSceneManager.OpenScene(ProjectPaths.BootScene, OpenSceneMode.Single);
            Debug.Log("[Setup] Core scenes regenerated.");
        }

        private static void BuildBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var systems = new GameObject("PersistentSystems");
            SceneManager.MoveGameObjectToScene(systems, scene);

            Bootstrapper bootstrapper = systems.AddComponent<Bootstrapper>();
            systems.AddComponent<SceneLoader>();
            systems.AddComponent<DebugOverlay>();

            var serialized = new SerializedObject(bootstrapper);
            serialized.FindProperty("firstScene").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameSceneDefinition>(ProjectPaths.TestSceneDefinition);
            serialized.FindProperty("sceneCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProjectPaths.SceneCatalog);
            serialized.FindProperty("inputReader").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputReader>(ProjectPaths.InputReader);
            serialized.FindProperty("logSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<LogSettings>(ProjectPaths.LogSettings);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ProjectPaths.BootScene);
        }

        private static void BuildTestScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            int groundLayer = LayerMask.NameToLayer("Ground");

            if (groundLayer >= 0)
            {
                ground.layer = groundLayer;
            }
            else
            {
                Debug.LogWarning("[Setup] No 'Ground' layer defined; leaving the plane on Default.");
            }


            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "OriginMarker";
            marker.transform.position = new Vector3(0f, 0.5f, 0f);

            var probe = new GameObject("SaveProbe");
            probe.AddComponent<SaveProbe>();

            Camera camera = Object.FindFirstObjectByType<Camera>();

            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 4f, -8f);
                camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            }

            EditorSceneManager.SaveScene(scene, ProjectPaths.TestScene);
        }

        public static void ConfigureBuildSettings()
        {
            string[] wanted = { ProjectPaths.BootScene, ProjectPaths.TestScene };
            var entries = new List<EditorBuildSettingsScene>(wanted.Length);

            foreach (string path in wanted)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogWarning($"[Setup] Scene missing, not added to build settings: {path}");
                    continue;
                }

                entries.Add(new EditorBuildSettingsScene(path, enabled: true));
            }

            EditorBuildSettings.scenes = entries.ToArray();
            Debug.Log($"[Setup] Build settings now contain {entries.Count} scene(s); Boot is first.");
        }
    }
}
