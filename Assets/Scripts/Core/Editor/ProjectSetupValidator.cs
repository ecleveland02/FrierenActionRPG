using Frieren.Core.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Core.EditorTools
{
    /// <summary>
    /// Warns once per editor session if the project is not configured the way the code expects.
    /// </summary>
    /// <remarks>
    /// Checks compile-time defines rather than PlayerSettings properties: the Input System package
    /// defines ENABLE_INPUT_SYSTEM only when it is the active handler, which is exactly the
    /// condition worth catching, and it cannot break against a different editor version.
    /// </remarks>
    [InitializeOnLoad]
    internal static class ProjectSetupValidator
    {
        private const string SessionKey = "Frieren.ProjectSetupValidator.HasRun";

        static ProjectSetupValidator()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += Validate;
        }

        private static void Validate()
        {
#if !ENABLE_INPUT_SYSTEM
            Debug.LogError(
                "[Setup] The Input System package is not the active input handler. " +
                "Set Project Settings > Player > Active Input Handling to 'Input System Package (New)' and restart.");
#endif

#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogWarning(
                "[Setup] Active Input Handling is set to 'Both'. The project standard is the Input System " +
                "package only; leaving the legacy manager on lets UnityEngine.Input calls slip in unnoticed.");
#endif

            if (AssetDatabase.LoadAssetAtPath<Object>(ProjectPaths.InputActions) == null)
            {
                Debug.LogWarning($"[Setup] Input actions asset missing at {ProjectPaths.InputActions}.");
            }
            else
            {
                WireInputReader();
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectPaths.BootScene) == null)
            {
                Debug.LogWarning(
                    $"[Setup] Boot scene missing at {ProjectPaths.BootScene}. " +
                    "Use Frieren > Setup > Regenerate Core Scenes.");
            }

            ValidateLayers();
        }

        /// <summary>
        /// Confirms the layer indices in <see cref="GameLayers"/> still name the layers they claim.
        /// </summary>
        /// <remarks>
        /// Layer masks are integers, so inserting a layer in Project Settings silently repoints
        /// every mask built from those constants. Nothing throws; enemies simply stop seeing the
        /// player and spells stop hitting anything, with no error to follow. This is the check that
        /// turns that into one line in the console.
        /// </remarks>
        private static void ValidateLayers()
        {
            (int index, string expected)[] expectations =
            {
                (GameLayers.Player, "Player"),
                (GameLayers.Enemy, "Enemy"),
                (GameLayers.Npc, "NPC"),
                (GameLayers.Ground, "Ground"),
                (GameLayers.Interactable, "Interactable"),
                (GameLayers.MagicTarget, "MagicTarget"),
                (GameLayers.Projectile, "Projectile"),
                (GameLayers.Trigger, "Trigger"),
            };

            foreach ((int index, string expected) in expectations)
            {
                string actual = LayerMask.LayerToName(index);

                if (actual != expected)
                {
                    Debug.LogError(
                        $"[Setup] Layer {index} is \"{actual}\" but GameLayers calls it \"{expected}\". " +
                        "Fix ProjectSettings > Tags and Layers, or GameLayers.cs - masks built from " +
                        "these constants are now pointing at the wrong things.");
                }
            }
        }

        /// <summary>
        /// Points the InputReader asset at the .inputactions asset if it is not wired up yet.
        /// </summary>
        /// <remarks>
        /// The reference cannot be committed by hand: a .inputactions file is imported by a
        /// ScriptedImporter which assigns the InputActionAsset a generated fileID, so the only
        /// place the correct value exists is inside the local Library. Assigning it once here is
        /// cheaper than asking every new clone of the repo to do it in the inspector, and it is a
        /// no-op on every subsequent load.
        /// </remarks>
        private static void WireInputReader()
        {
            var reader = AssetDatabase.LoadAssetAtPath<InputReader>(ProjectPaths.InputReader);

            if (reader == null)
            {
                return;
            }

            var serialized = new SerializedObject(reader);
            SerializedProperty actions = serialized.FindProperty("actions");

            if (actions == null || actions.objectReferenceValue != null)
            {
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectPaths.InputActions);

            if (asset == null)
            {
                return;
            }

            actions.objectReferenceValue = asset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(reader);
            Debug.Log($"[Setup] Linked {ProjectPaths.InputReader} to {ProjectPaths.InputActions}.");
        }
    }
}
