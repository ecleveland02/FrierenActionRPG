using System.Collections.Generic;
using System.Text;
using Frieren.Core.Persistence;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frieren.Core.EditorTools
{
    /// <summary>
    /// Checks that every persistent object in the open scenes has an id, and that no two share one.
    /// </summary>
    /// <remarks>
    /// The two ways an authored id fails, and both are silent. A blank id means the object never
    /// registers and simply does not persist; a duplicate means two objects share a save entry and
    /// the second one loaded wins, so a door opens because a crate burned. Neither produces an
    /// error at runtime, and both are nearly impossible to reason backwards from.
    ///
    /// Runs on save and on demand rather than every frame: this walks the whole scene, and the
    /// mistake it catches is made when authoring, not when playing.
    /// </remarks>
    internal static class SceneObjectIdValidator
    {
        private const string MenuPath = "Frieren/Validate/Scene Object Ids";

        [MenuItem(MenuPath)]
        private static void ValidateFromMenu()
        {
            if (Validate(out string report))
            {
                Debug.Log($"[Setup] Scene object ids are fine.\n{report}");
                return;
            }

            Debug.LogError($"[Setup] Scene object ids need attention.\n{report}");
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorSceneManagerHook();

        private static void EditorSceneManagerHook()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += _ =>
            {
                if (!Validate(out string report))
                {
                    Debug.LogError($"[Setup] Scene object ids need attention.\n{report}");
                }
            };
        }

        private static bool Validate(out string report)
        {
            var seen = new Dictionary<string, string>();
            var problems = new List<string>();
            int checkedCount = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (SceneObjectId id in root.GetComponentsInChildren<SceneObjectId>(true))
                    {
                        checkedCount++;
                        string path = $"{scene.name}/{Path(id.transform)}";

                        if (!id.HasId)
                        {
                            // A spawner fills these in at runtime, so an id-less prefab instance is
                            // only a problem when it is sitting in a scene.
                            problems.Add($"  blank id on {path}");
                            continue;
                        }

                        if (seen.TryGetValue(id.Id, out string other))
                        {
                            problems.Add($"  '{id.Id}' is used by both {other} and {path}");
                            continue;
                        }

                        seen[id.Id] = path;
                    }
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine($"{checkedCount} object(s) checked, {seen.Count} distinct id(s).");

            for (int i = 0; i < problems.Count; i++)
            {
                builder.AppendLine(problems[i]);
            }

            report = builder.ToString().TrimEnd();
            return problems.Count == 0;
        }

        private static string Path(Transform transform)
        {
            string path = transform.name;

            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = $"{parent.name}/{path}";
            }

            return path;
        }
    }
}
