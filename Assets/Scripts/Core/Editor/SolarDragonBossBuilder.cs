using System;
using System.IO;
using System.Linq;
using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Core.Persistence;
using Frieren.Enemies;
using Frieren.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    public static class SolarDragonBossBuilder
    {
        private const string Folder = "Assets/Art/Bosses/SolarDragon";
        private const string ModelPath = Folder + "/SolarDragon_Gameplay.fbx";
        private const string PrefabPath = "Assets/Prefabs/Characters/Boss_SolarDragon.prefab";
        private const string ScenePath = "Assets/Scenes/FrierenOpenWorld.unity";

        [MenuItem("Frieren/Bosses/Build Solar Dragon Encounter", priority = 80)]
        public static void Build()
        {
            ConfigureImporter();
            ConfigureBossMusic();
            Material body = Material("SolarDragon_Body", new Color(.72f, .23f, .08f),
                AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/SolarDragon_Albedo.png"));
            string metallicPath = Folder + "/SolarDragon_MetallicSmoothness.png";
            var metallicImporter = (TextureImporter)AssetImporter.GetAtPath(metallicPath);
            metallicImporter.sRGBTexture = false;
            metallicImporter.SaveAndReimport();
            body.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath));
            body.EnableKeyword("_METALLICGLOSSMAP");
            body.SetFloat("_Metallic", 1f);
            Material mouth = Material("SolarDragon_Mouth", new Color(.22f, .025f, .035f), null);
            Material tongue = Material("SolarDragon_Tongue", new Color(.48f, .12f, .18f), null);
            Material ivory = Material("SolarDragon_Ivory", new Color(.92f, .82f, .59f), null);
            AnimatorController controller = BuildController();
            CharacterStatsDefinition stats = BuildStats();
            GameObject prefab = BuildPrefab(body, mouth, tongue, ivory, controller, stats);
            PlaceEncounter(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[Solar Dragon] Built six clips, boss prefab, health bar and open-world arena encounter.");
        }

        private static void ConfigureBossMusic()
        {
            const string path = "Assets/Audio/Bosses/SolarDragon_Zoltraak.mp3";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new FileNotFoundException(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .72f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        [MenuItem("Frieren/Bosses/Validate Solar Dragon Encounter", priority = 81)]
        public static void Validate()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Dragon model is missing.");
            string[] required = { "Dragon_Idle", "Dragon_Walk", "Dragon_Bite", "Dragon_WingBuffet", "Dragon_TailSwipe", "Dragon_Death" };
            foreach (string clip in required) Clip(clip);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<EnemyBrain>() == null || prefab.GetComponent<BossHealthDisplay>() == null ||
                prefab.GetComponent<SolarDragonBossAnimation>() == null || prefab.GetComponent<BossMusicCue>() == null)
                throw new InvalidOperationException("Dragon boss prefab is incomplete.");
            var music = new SerializedObject(prefab.GetComponent<BossMusicCue>());
            if (music.FindProperty("music").objectReferenceValue == null)
                throw new InvalidOperationException("Dragon boss music is not assigned.");
            if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length < 5)
                throw new InvalidOperationException("Dragon skinned meshes are missing.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject arena = GameObject.Find("Solar Dragon Arena");
            EnemySpawner spawner = arena != null ? arena.GetComponentInChildren<EnemySpawner>(true) : null;
            if (spawner == null) throw new InvalidOperationException("Open world has no Solar Dragon spawner.");
            var serialized = new SerializedObject(spawner);
            if (serialized.FindProperty("enemyPrefab").objectReferenceValue != prefab)
                throw new InvalidOperationException("Solar Dragon spawner references the wrong prefab.");
            Debug.Log("[Solar Dragon] Validation passed: six clips, five skinned meshes, boss gameplay and arena spawner.");
        }

        private static void ConfigureImporter()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 8;
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildController()
        {
            string path = Folder + "/SolarDragon.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            Add(machine, "Idle", Clip("Dragon_Idle"), true);
            Add(machine, "Walk", Clip("Dragon_Walk"));
            Add(machine, "Attack_Bite", Clip("Dragon_Bite"));
            Add(machine, "Attack_Wing", Clip("Dragon_WingBuffet"));
            Add(machine, "Attack_Tail", Clip("Dragon_TailSwipe"));
            Add(machine, "Hit", Clip("Dragon_WingBuffet"));
            Add(machine, "Death", Clip("Dragon_Death"));
            return controller;
        }

        private static AnimationClip Clip(string suffix)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__") && candidate.name.EndsWith(suffix));
            return clip != null ? clip : throw new InvalidOperationException("Missing dragon clip " + suffix);
        }

        private static void Add(AnimatorStateMachine machine, string name, Motion motion, bool initial = false)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = motion;
            if (initial) machine.defaultState = state;
        }

        private static CharacterStatsDefinition BuildStats()
        {
            string path = Folder + "/SolarDragonStats.asset";
            var stats = AssetDatabase.LoadAssetAtPath<CharacterStatsDefinition>(path);
            if (stats == null) { stats = ScriptableObject.CreateInstance<CharacterStatsDefinition>(); AssetDatabase.CreateAsset(stats, path); }
            Set(stats, "id", "boss.solar-dragon");
            Set(stats, "maxHealth", 1200f);
            Set(stats, "healthRegenPerSecond", 0f);
            Set(stats, "maxMana", 0f);
            EditorUtility.SetDirty(stats);
            return stats;
        }

        private static GameObject BuildPrefab(Material body, Material mouth, Material tongue, Material ivory,
            RuntimeAnimatorController controller, CharacterStatsDefinition stats)
        {
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemy_Sentinel.prefab"));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Boss_SolarDragon";
            Object.DestroyImmediate(root.GetComponent<PlaceholderCharacterAnimation>());
            Object.DestroyImmediate(root.GetComponent<DeathSink>());
            Transform oldVisual = root.transform.Find("Visual");
            if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = "SolarDragonVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 1.35f;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                string lower = renderer.name.ToLowerInvariant();
                if (lower.Contains("teeth")) renderer.sharedMaterials = new[] { ivory };
                else if (lower.Contains("tongue")) renderer.sharedMaterials = new[] { tongue };
                else if (lower.Contains("body") || lower.Contains("jaw"))
                    renderer.sharedMaterials = renderer.sharedMaterials.Length > 1
                        ? new[] { body, mouth } : new[] { body };
                else renderer.sharedMaterials = new[] { body };
                renderer.gameObject.layer = GameLayers.Enemy;
            }
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var characterController = root.GetComponent<CharacterController>();
            characterController.radius = 2.1f; characterController.height = 3.4f;
            characterController.center = new Vector3(0f, 1.7f, 0f); characterController.stepOffset = .5f;
            Set(root.GetComponent<CharacterStats>(), "definition", stats);
            Set(root.GetComponent<EnemyBrain>(), "moveSpeed", 4.4f);
            Set(root.GetComponent<EnemyBrain>(), "turnSpeed", 170f);
            Set(root.GetComponent<EnemyBrain>(), "standoff", .8f);
            Set(root.GetComponent<EnemyBrain>(), "staggerDuration", .16f);
            Set(root.GetComponent<EnemyBrain>(), "staggerCooldown", 2.5f);
            Set(root.GetComponent<EnemyBrain>(), "corpseDuration", 12f);
            Set(root.GetComponent<EnemyPerception>(), "eyeHeight", 2.7f);
            Set(root.GetComponent<EnemyPerception>(), "sightRange", 34f);
            Set(root.GetComponent<EnemyPerception>(), "fieldOfView", 300f);
            Set(root.GetComponent<EnemyPerception>(), "loseRange", 52f);
            Set(root.GetComponent<EnemyMelee>(), "range", 5.4f);
            Set(root.GetComponent<EnemyMelee>(), "hitRadius", 2.8f);
            Set(root.GetComponent<EnemyMelee>(), "hitForwardOffset", 2.6f);
            Set(root.GetComponent<EnemyMelee>(), "windUp", .72f);
            Set(root.GetComponent<EnemyMelee>(), "recovery", .7f);
            Set(root.GetComponent<EnemyMelee>(), "cooldown", 1.8f);
            Set(root.GetComponent<EnemyMelee>(), "damage", 28f);
            Set(root.AddComponent<SolarDragonBossAnimation>(), "animator", animator);
            root.AddComponent<BossHealthDisplay>();
            Set(root.AddComponent<BossMusicCue>(), "music",
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bosses/SolarDragon_Zoltraak.mp3"));
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = GameLayers.Enemy;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void PlaceEncounter(GameObject bossPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject old = GameObject.Find("Solar Dragon Arena");
            if (old != null) Object.DestroyImmediate(old);
            var arena = new GameObject("Solar Dragon Arena");
            Vector3 centre = Ground(new Vector3(1750f, 0f, 650f));
            arena.transform.position = centre;
            var spawnerObject = new GameObject("Solar Dragon Spawner");
            spawnerObject.transform.SetParent(arena.transform, false);
            var id = spawnerObject.AddComponent<SceneObjectId>();
            Set(id, "id", "openworld.solar-dragon-spawner");
            var spawner = spawnerObject.AddComponent<EnemySpawner>();
            Set(spawner, "enemyPrefab", bossPrefab);
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI * 2f / 14f;
                var stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stone.name = "Arena Stone " + (i + 1);
                stone.transform.SetParent(arena.transform);
                Vector3 position = centre + new Vector3(Mathf.Cos(angle) * 24f, 0f, Mathf.Sin(angle) * 24f);
                stone.transform.position = Ground(position) + Vector3.up * 1.5f;
                stone.transform.localScale = new Vector3(2.4f, 3f + (i % 3), 2.4f);
                stone.transform.rotation = Quaternion.Euler(i % 2 * 4f, -angle * Mathf.Rad2Deg, (i % 3 - 1) * 3f);
                stone.GetComponent<Renderer>().sharedMaterial = Material("SolarDragon_ArenaStone", new Color(.25f, .22f, .18f), null);
                stone.layer = GameLayers.Ground;
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Vector3 Ground(Vector3 position)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position; Vector3 size = terrain.terrainData.size;
                if (position.x >= origin.x && position.x <= origin.x + size.x && position.z >= origin.z && position.z <= origin.z + size.z)
                { position.y = origin.y + terrain.SampleHeight(position); return position; }
            }
            return position;
        }

        private static Material Material(string name, Color color, Texture texture)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; material.mainTexture = texture; material.SetFloat("_Glossiness", .24f);
            EditorUtility.SetDirty(material); return material;
        }

        private static void Set(Object target, string name, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name) ?? throw new InvalidOperationException(target.GetType().Name + " has no field " + name);
            if (value is Object reference) property.objectReferenceValue = reference;
            else if (value is string text) property.stringValue = text;
            else if (value is float number) property.floatValue = number;
            else throw new InvalidOperationException("Unsupported serialized value for " + name);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
