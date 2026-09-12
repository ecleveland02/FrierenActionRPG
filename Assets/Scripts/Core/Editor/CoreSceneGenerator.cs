using System.Collections.Generic;
using System.IO;
using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Core.Bootstrap;
using Frieren.Core.Debugging;
using Frieren.Core.Persistence;
using Frieren.Core.Input;
using Frieren.Core.Scenes;
using Frieren.Magic;
using Frieren.Player;
using Frieren.World;
using Frieren.Player.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frieren.Core.EditorTools
{
    /// <summary>
    /// Rebuilds the player prefab and the Boot and Test scenes from code.
    /// </summary>
    /// <remarks>
    /// These assets are checked in, so this is not needed day to day. It exists because a scene or
    /// prefab is the one kind of asset that cannot be reviewed in a diff, and a corrupted one is
    /// otherwise unrecoverable without redoing the wiring by hand.
    ///
    /// It is also the authoritative, reviewable description of what those assets contain - which
    /// only holds while it is kept in step with them. Changing the committed scene by hand without
    /// changing this is how the safety net quietly stops being one.
    /// </remarks>
    internal static class CoreSceneGenerator
    {
        public static void RegenerateAll()
        {
            CoreAssetFactory.EnsureAll();
            Directory.CreateDirectory(ProjectPaths.ScenesFolder);

            BuildPlayerPrefab();
            BuildBootScene();
            BuildTestScene();

            AssetDatabase.Refresh();
            ConfigureBuildSettings();

            EditorSceneManager.OpenScene(ProjectPaths.BootScene, OpenSceneMode.Single);
            Debug.Log("[Setup] Player prefab and core scenes regenerated.");
        }

        // ------------------------------------------------------------------ player

        public static GameObject BuildPlayerPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectPaths.PlayerPrefab) ?? "Assets");

            var reader = AssetDatabase.LoadAssetAtPath<InputReader>(ProjectPaths.InputReader);
            var root = new GameObject("Player");
            int playerLayer = ResolveLayer("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;

            root.AddComponent<CharacterMotor>();
            root.AddComponent<CharacterActionLock>();

            CharacterStats stats = root.AddComponent<CharacterStats>();
            AssignReference(stats, "definition",
                AssetDatabase.LoadAssetAtPath<CharacterStatsDefinition>(ProjectPaths.PlayerStats));
            root.AddComponent<CharacterHealth>();
            root.AddComponent<CharacterMana>();
            SceneObjectId objectId = root.AddComponent<SceneObjectId>();
            objectId.Assign("character.player");
            root.AddComponent<PersistentObject>();
            root.AddComponent<CharacterPersistence>();
            root.AddComponent<CharacterVitalsReadout>();
            root.AddComponent<CharacterSpellcaster>();
            CharacterLevitation levitation = root.AddComponent<CharacterLevitation>();
            var levitationSerialized = new SerializedObject(levitation);
            levitationSerialized.FindProperty("maximumRise").floatValue = 8f;
            levitationSerialized.FindProperty("riseSpeed").floatValue = 4f;
            levitationSerialized.ApplyModifiedPropertiesWithoutUndo();

            PlayerSpellInput spellInput = root.AddComponent<PlayerSpellInput>();
            AssignReference(spellInput, "inputReader", reader);
            AssignSpellList(spellInput);

            SpellWheelInput spellWheel = root.AddComponent<SpellWheelInput>();
            AssignReference(spellWheel, "inputReader", reader);

            PlayerBlockInput block = root.AddComponent<PlayerBlockInput>();
            AssignReference(block, "inputReader", reader);
            AssignReference(block, "blockSpell",
                AssetDatabase.LoadAssetAtPath<SpellDefinition>(ProjectPaths.SpellBarrier));

            PlayerLocomotion locomotion = root.AddComponent<PlayerLocomotion>();
            PlayerDodge dodge = root.AddComponent<PlayerDodge>();
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
            PlaceholderCharacterAnimation placeholder = root.AddComponent<PlaceholderCharacterAnimation>();

            AssignReference(locomotion, "inputReader", reader);
            AssignReference(dodge, "inputReader", reader);
            AssignReference(interactor, "inputReader", reader);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);

            // A primitive brings its own collider, which would fight the CharacterController for
            // the same space. The controller is the character's only collider.
            StripColliders(visual);

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.transform.SetParent(visual.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.35f, 0.42f);
            nose.transform.localScale = new Vector3(0.28f, 0.28f, 0.45f);
            StripColliders(nose);

            AssignReference(placeholder, "visual", visual.transform);
            AssignReference(placeholder, "tintTarget", visual.GetComponent<Renderer>());

            SetLayerRecursively(root, playerLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectPaths.PlayerPrefab);
            Object.DestroyImmediate(root);

            return prefab;
        }

        // ------------------------------------------------------------------- scenes

        private static void BuildBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var systems = new GameObject("PersistentSystems");
            SceneManager.MoveGameObjectToScene(systems, scene);

            Bootstrapper bootstrapper = systems.AddComponent<Bootstrapper>();
            systems.AddComponent<SceneLoader>();
            systems.AddComponent<DebugOverlay>();

            AssignReference(bootstrapper, "firstScene",
                AssetDatabase.LoadAssetAtPath<GameSceneDefinition>(ProjectPaths.TestSceneDefinition));
            AssignReference(bootstrapper, "sceneCatalog",
                AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProjectPaths.SceneCatalog));
            AssignReference(bootstrapper, "inputReader",
                AssetDatabase.LoadAssetAtPath<InputReader>(ProjectPaths.InputReader));
            AssignReference(bootstrapper, "logSettings",
                AssetDatabase.LoadAssetAtPath<LogSettings>(ProjectPaths.LogSettings));

            EditorSceneManager.SaveScene(scene, ProjectPaths.BootScene);
        }

        private static void BuildTestScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var reader = AssetDatabase.LoadAssetAtPath<InputReader>(ProjectPaths.InputReader);
            int groundLayer = ResolveLayer("Ground");
            int interactableLayer = ResolveLayer("Interactable");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.layer = groundLayer;

            CreateBox("Platform", new Vector3(7f, 0.75f, 3f), new Vector3(5f, 1.5f, 5f), groundLayer);
            CreateBox("Step", new Vector3(3.5f, 0.15f, 3f), new Vector3(2f, 0.3f, 2f), groundLayer);
            CreateBox("WallForCameraCollision", new Vector3(-6f, 2f, 2f), new Vector3(0.5f, 4f, 8f), groundLayer);

            CreateInteractable("Interactable_Lever", new Vector3(2.5f, 0.4f, -2.5f), "Pull", interactableLayer);
            CreateInteractable("Interactable_Crate", new Vector3(-2.5f, 0.4f, -2.5f), "Open", interactableLayer);

            var probe = new GameObject("SaveProbe");
            probe.AddComponent<SaveProbe>();

            int magicTargetLayer = ResolveLayer("MagicTarget");
            CreateFlammable("Flammable_Crate_1", new Vector3(-4.5f, 0.5f, 4.5f), magicTargetLayer);
            CreateFlammable("Flammable_Crate_2", new Vector3(-3f, 0.5f, 4.5f), magicTargetLayer);
            CreateLevitatable("Levitatable_Block", new Vector3(4f, 0.5f, 6.5f), magicTargetLayer);
            CreateTargetDummy("TargetDummy", new Vector3(0f, 1f, 7f), magicTargetLayer);

            OrbitCameraRig rig = null;
            UnityEngine.Camera camera = Object.FindFirstObjectByType<UnityEngine.Camera>();

            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 4f, -8f);
                camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                camera.nearClipPlane = 0.15f;
                rig = camera.gameObject.AddComponent<OrbitCameraRig>();
                AssignReference(rig, "inputReader", reader);
            }

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(0f, 0.1f, -3f);
            PlayerSpawner spawner = spawn.AddComponent<PlayerSpawner>();
            AssignReference(spawner, "playerPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PlayerPrefab));
            AssignReference(spawner, "cameraRig", rig);

            EditorSceneManager.SaveScene(scene, ProjectPaths.TestScene);
        }

        // -------------------------------------------------------------------- utils

        private static void AssignSpellList(PlayerSpellInput spellInput)
        {
            var serialized = new SerializedObject(spellInput);
            SerializedProperty spells = serialized.FindProperty("knownSpells");
            spells.ClearArray();

            // Wheel order, which is also the number row. Barrier is deliberately absent: it lives
            // on the block button, not in a slot you have to select before you can defend yourself.
            string[] paths =
            {
                ProjectPaths.SpellArcaneBolt,
                ProjectPaths.SpellZoltraak,
                ProjectPaths.SpellFire,
                ProjectPaths.SpellIce,
                ProjectPaths.SpellLevitate,
                ProjectPaths.SpellWater,
                ProjectPaths.SpellRepair,
                ProjectPaths.SpellUnlock,
            };

            foreach (string path in paths)
            {
                var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);

                if (spell == null)
                {
                    Debug.LogWarning($"[Setup] Spell asset missing, not added to the player: {path}");
                    continue;
                }

                spells.InsertArrayElementAtIndex(spells.arraySize);
                spells.GetArrayElementAtIndex(spells.arraySize - 1).objectReferenceValue = spell;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateFlammable(string name, Vector3 position, int layer)
        {
            GameObject box = CreateBox(name, position, Vector3.one, layer);
            box.tag = "MagicTarget";
            FlammableObject flammable = box.AddComponent<FlammableObject>();
            AssignReference(flammable, "tintTarget", box.GetComponent<Renderer>());
        }

        private static void CreateLevitatable(string name, Vector3 position, int layer)
        {
            GameObject box = CreateBox(name, position, new Vector3(1.6f, 1f, 1.6f), layer);
            box.tag = "MagicTarget";
            LevitatableObject levitatable = box.AddComponent<LevitatableObject>();

            // Tuned for a held cast: the target height climbs at about the rise speed, so how high
            // it goes tracks how long the button is held, and the hold lapses shortly after release.
            var serialized = new SerializedObject(levitatable);
            serialized.FindProperty("liftPerPulse").floatValue = 0.2f;
            serialized.FindProperty("holdDuration").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A character built from the Milestone 3 components, so damage spells have a subject.</summary>
        private static void CreateTargetDummy(string name, Vector3 position, int layer)
        {
            GameObject box = CreateBox(name, position, new Vector3(1f, 2f, 1f), layer);
            box.tag = "MagicTarget";

            CharacterStats stats = box.AddComponent<CharacterStats>();
            AssignReference(stats, "definition",
                AssetDatabase.LoadAssetAtPath<CharacterStatsDefinition>(ProjectPaths.PlayerStats));
            box.AddComponent<CharacterHealth>();
            box.AddComponent<CharacterMana>();

            CharacterVitalsReadout readout = box.AddComponent<CharacterVitalsReadout>();
            var serialized = new SerializedObject(readout);
            serialized.FindProperty("showKeys").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, int layer)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.layer = layer;
            return box;
        }

        private static void CreateInteractable(string name, Vector3 position, string prompt, int layer)
        {
            GameObject box = CreateBox(name, position, Vector3.one * 0.8f, layer);
            box.tag = "Interactable";

            DebugInteractable interactable = box.AddComponent<DebugInteractable>();
            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("prompt").stringValue = prompt;
            serialized.FindProperty("tintTarget").objectReferenceValue = box.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StripColliders(GameObject target)
        {
            foreach (Collider collider in target.GetComponents<Collider>())
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;

            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>Falls back to Default rather than assigning -1, which Unity rejects.</summary>
        private static int ResolveLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer >= 0)
            {
                return layer;
            }

            Debug.LogWarning($"[Setup] No '{layerName}' layer defined; falling back to Default.");
            return 0;
        }

        private static void AssignReference(Object component, string propertyName, Object value)
        {
            if (component == null)
            {
                return;
            }

            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning($"[Setup] {component.GetType().Name} has no serialized field '{propertyName}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
