using System;
using System.Collections.Generic;
using System.IO;
using Frieren.Characters.Animation;
using Frieren.Core.Persistence;
using Frieren.Enemies;
using Frieren.Presentation;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    /// <summary>Dresses Claude's existing puzzle geometry without regenerating the core scenes.</summary>
    public static class WatchtowerWorldBuilder
    {
        private const string ScenePath = "Assets/Scenes/Watchtower.unity";
        private const string Output = "Assets/Art/World/Watchtower";
        private const string Nature = "Assets/Art/Polytope Studio/Lowpoly_Environments/Prefabs/";
        private const string Camp = "Assets/Stylized_Labs/Stylized_Fantasy/Props_Sample/Prefabs/";
        private const string Monster = "Assets/Stylized3DMonster/Monster04/";
        private static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        private static Transform dressing;

        [MenuItem("Frieren/World/Build Watchtower Woodland")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Require<GameObject>(Nature + "Trees/PT_Pine_Tree_03_green.prefab");
            Require<GameObject>(Camp + "SM_Tent.prefab");
            Require<GameObject>(Monster + "Prefab/Monster04_01.prefab");
            EnsureFolder(Output + "/Materials");
            converted.Clear();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // A first-run copy is outside Assets: recoverable, but not imported or shipped.
            Directory.CreateDirectory("ArtSource/WorldBackups");
            const string backup = "ArtSource/WorldBackups/Watchtower-before-woodland.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            var previous = GameObject.Find("WorldDressing");
            if (previous != null) Object.DestroyImmediate(previous);
            dressing = new GameObject("WorldDressing").transform;

            Material grass = Solid("Moss Ground", new Color(0.25f, 0.35f, 0.20f));
            Material stone = Solid("Weathered Stone", new Color(0.43f, 0.46f, 0.40f));
            Material darkStone = Solid("Foundation Stone", new Color(0.28f, 0.32f, 0.29f));
            Material path = Solid("Warm Trail", new Color(0.49f, 0.42f, 0.28f));
            GameObject ground = GameObject.Find("Ground");
            ground.transform.position = new Vector3(0f, 0f, 5f);
            ground.transform.localScale = new Vector3(8f, 1f, 10f);
            ground.GetComponent<Renderer>().sharedMaterial = grass;
            GameObject.Find("Gate_Panel").GetComponent<Renderer>().sharedMaterial = Solid("Old Gate Timber", new Color(0.33f, 0.22f, 0.12f));
            GameObject.Find("PlayerSpawn").transform.position = new Vector3(0f, 0.2f, -36f);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("Wall_") || root.name.StartsWith("Tower_") ||
                    root.name.StartsWith("Cover_") || root.name.StartsWith("Mezzanine_") ||
                    root.name.StartsWith("Walkway_") || root.name == "Gate_Lintel")
                {
                    if (root.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = stone;
                }
            }

            // Broad readable trail, with a quiet camp before the first encounter.
            BuildTrail(path);
            Prop(Camp + "SM_Tent.prefab", new Vector3(-7f, 0f, -32f), 3.8f, 110f, true);
            Prop(Camp + "SM_Campfire.prefab", new Vector3(-4.5f, 0f, -29f), 1.1f, 0f, false);
            Prop(Camp + "SM_Bench.prefab", new Vector3(-6.5f, 0f, -28f), 0.8f, 70f, true);
            Prop(Camp + "SM_Barrel.prefab", new Vector3(-9f, 0f, -30f), 1.1f, 0f, true);
            Prop(Camp + "SM_Box.prefab", new Vector3(-8f, 0f, -29f), 0.8f, 15f, true);
            Fire(new Vector3(-4.5f, 0.5f, -29f));

            var random = new System.Random(7101);
            for (int i = 0; i < 90; i++)
            {
                float z = -42f + (float)random.NextDouble() * 94f;
                float x = (i % 2 == 0 ? -1f : 1f) * (19f + (float)random.NextDouble() * 17f);
                if (z < -3f) x = (i % 2 == 0 ? -1f : 1f) * (11f + (float)random.NextDouble() * 22f);
                Prop(Nature + "Trees/PT_Pine_Tree_03_green.prefab", new Vector3(x, 0f, z),
                    6f + (float)random.NextDouble() * 5f, (float)random.NextDouble() * 360f, true, true);
            }
            for (int i = 0; i < 30; i++)
            {
                float z = -40f + (float)random.NextDouble() * 90f;
                float x = (i % 2 == 0 ? -1f : 1f) * (19f + (float)random.NextDouble() * 17f);
                Prop(Nature + "Rocks/PT_Generic_Rock_01.prefab", new Vector3(x, -0.15f, z),
                    1.5f + (float)random.NextDouble() * 3f, i * 73f, true);
            }
            for (int i = 0; i < 55; i++)
            {
                float z = -40f + (float)random.NextDouble() * 37f;
                float x = (i % 2 == 0 ? -1f : 1f) * (4.5f + (float)random.NextDouble() * 5f);
                if (z < -25f && x < 0f) continue;
                Prop(Nature + "Plants/PT_Grass_02.prefab", new Vector3(x, 0f, z), 0.45f + (float)random.NextDouble() * 0.4f, i * 29f, false);
            }
            // Buttresses, caps and crenellations articulate the original blockout, with no route changes.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int z = 2; z <= 22; z += 5)
                {
                    Primitive("Courtyard Buttress", PrimitiveType.Cube, new Vector3(side * 15f, 2.6f, z), new Vector3(1.8f, 5.2f, 1.3f), darkStone);
                    Primitive("Wall Crown", PrimitiveType.Cube, new Vector3(side * 15f, 5.25f, z), new Vector3(1.9f, 0.7f, 2f), stone);
                }
                for (int z = 26; z <= 45; z += 6)
                    Primitive("Tower Buttress", PrimitiveType.Cube, new Vector3(side * 8.5f, 5.6f, z), new Vector3(1.5f, 11.2f, 1.4f), darkStone);
                Primitive("Tower Cornice", PrimitiveType.Cube, new Vector3(side * 8f, 10.6f, 35f), new Vector3(1.8f, 0.65f, 23f), stone);
                for (int z = 25; z < 47; z += 3)
                    Primitive("Tower Battlement", PrimitiveType.Cube, new Vector3(side * 8f, 11.5f, z), new Vector3(1.5f, 1f, 1.4f), darkStone);
            }
            for (int x = -14; x <= 14; x += 3)
            {
                if (Mathf.Abs(x) < 3) continue;
                Primitive("Gate Battlement", PrimitiveType.Cube, new Vector3(x, 5.4f, 0f), new Vector3(1.4f, 1f, 1.4f), darkStone);
            }
            for (int i = 0; i < 7; i++)
                Prop(Nature + "Rocks/PT_Generic_Rock_01.prefab", new Vector3(-11f + i * 1.5f, 0f, 20f), 0.8f + i % 3 * 0.3f, i * 37f, true);
            Prop(Camp + "SM_Stone_Obelisk.prefab", new Vector3(9.5f, 0f, -5f), 3f, -15f, true);

            // Solid boundaries prevent falling off the small playable footprint.
            Boundary(new Vector3(-39f, 3f, 5f), new Vector3(1f, 8f, 100f));
            Boundary(new Vector3(39f, 3f, 5f), new Vector3(1f, 8f, 100f));
            Boundary(new Vector3(0f, 3f, -44f), new Vector3(80f, 8f, 1f));
            Boundary(new Vector3(0f, 3f, 54f), new Vector3(80f, 8f, 1f));

            GameObject enemy = BuildEnemy();
            var courtyard = GameObject.Find("SentinelSpawner").GetComponent<EnemySpawner>();
            Set(courtyard, "enemyPrefab", enemy);
            var approach = new GameObject("Woodland Encounter");
            approach.transform.SetParent(dressing);
            approach.AddComponent<SceneObjectId>().Assign("watchtower.woodland.encounter");
            approach.AddComponent<PersistentObject>();
            var spawner = approach.AddComponent<EnemySpawner>();
            Set(spawner, "enemyPrefab", enemy);
            var points = new Transform[2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GameObject("Woodland Spawn " + (i + 1)).transform;
                points[i].SetParent(approach.transform);
                points[i].position = new Vector3(i == 0 ? -3f : 4f, 0.2f, i == 0 ? -14f : -10f);
                points[i].rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            SetArray(spawner, "spawnPoints", points);
            Soundtrack(courtyard, spawner);
            Lighting();
            var navigation = new GameObject("Baked Ground Navigation").AddComponent<NavMeshSurface>();
            navigation.transform.SetParent(dressing);
            navigation.collectObjects = CollectObjects.All;
            navigation.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navigation.layerMask = GameLayers.SolidMask;
            navigation.BuildNavMesh();
            const string navPath = Output + "/WoodlandNavigation.asset";
            var savedNav = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (savedNav == null) AssetDatabase.CreateAsset(navigation.navMeshData, navPath);
            else
            {
                NavMeshData temporary = navigation.navMeshData;
                navigation.RemoveData();
                EditorUtility.CopySerialized(temporary, savedNav);
                navigation.navMeshData = savedNav;
                Object.DestroyImmediate(temporary);
                navigation.AddData();
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ArchiveCharacterScenes();
            RenderPreview();
            Debug.Log("[Woodland] Built forest camp, two encounters, soundtrack, navigation and ruin dressing. " +
                "Build stamp: " + Debugging.BuildStamp.Current + ".");
        }

        private static void ArchiveCharacterScenes()
        {
            EnsureFolder("Assets/Scenes/Archive");
            foreach (string name in new[] { "KaelTestScene.unity", "KaelClothTestScene.unity" })
            {
                string source = "Assets/Scenes/" + name;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null) continue;
                string error = AssetDatabase.MoveAsset(source, "Assets/Scenes/Archive/" + name);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
        }

        private static void BuildTrail(Material material)
        {
            const int segments = 24;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float center = Mathf.Sin(t * Mathf.PI * 2f) * 1.2f;
                float width = 2.5f + Mathf.Sin(t * 9f) * 0.25f;
                vertices[i * 2] = new Vector3(center - width, 0.025f, -43f + t * 43f);
                vertices[i * 2 + 1] = new Vector3(center + width, 0.025f, -43f + t * 43f);
                uv[i * 2] = new Vector2(0f, t * 10f);
                uv[i * 2 + 1] = new Vector2(1f, t * 10f);
                if (i == segments) continue;
                int v = i * 2;
                int index = i * 6;
                triangles[index] = v;
                triangles[index + 1] = v + 2;
                triangles[index + 2] = v + 1;
                triangles[index + 3] = v + 1;
                triangles[index + 4] = v + 2;
                triangles[index + 5] = v + 3;
            }
            string path = Output + "/WoodlandTrail.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = "Woodland Trail";
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var trail = new GameObject("Woodland Trail");
            trail.transform.SetParent(dressing, false);
            trail.AddComponent<MeshFilter>().sharedMesh = mesh;
            trail.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(trail, StaticEditorFlags.BatchingStatic);
        }

        private static GameObject BuildEnemy()
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>("Assets/Prefabs/Characters/Enemy_Sentinel.prefab"));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Enemy_WoodlandSentinel";
            Object.DestroyImmediate(root.GetComponent<PlaceholderCharacterAnimation>());
            Object.DestroyImmediate(root.GetComponent<DeathSink>());
            Object.DestroyImmediate(root.transform.Find("Visual").gameObject);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(Monster + "Prefab/Monster04_01.prefab"));
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.transform.SetParent(root.transform, false);
            Normalize(visual, 2f);
            ConvertMaterials(visual);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Transform model = visual.transform.Find("Monster04_AllAnim");
                if (model == null || model.Find("root") == null) throw new InvalidOperationException("Monster04 clip root is missing.");
                animator = model.gameObject.AddComponent<Animator>();
            }
            string controllerPath = Output + "/WoodlandMonster.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var states = new[] { "Idle", "Run", "Attack", "Hit", "Death" };
                var clips = new[] { "Idle", "Run", "Attack01", "GetHit", "Die" };
                for (int i = 0; i < states.Length; i++)
                {
                    var state = controller.layers[0].stateMachine.AddState(states[i]);
                    state.motion = Require<AnimationClip>(Monster + "Anim/InPlace_Anim/Monster04_" + clips[i] + ".anim");
                    if (i == 0) controller.layers[0].stateMachine.defaultState = state;
                }
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            Set(root.AddComponent<WorldEnemyAnimation>(), "animator", animator);
            root.AddComponent<EnemyNavigation>();
            var telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telegraph.name = "Attack Telegraph Source";
            telegraph.transform.SetParent(root.transform, false);
            telegraph.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            telegraph.transform.localScale = new Vector3(2f, 0.02f, 2f);
            Object.DestroyImmediate(telegraph.GetComponent<Collider>());
            telegraph.GetComponent<Renderer>().sharedMaterial = Solid("Attack Telegraph", new Color(1f, 0.5f, 0.08f));
            telegraph.GetComponent<Renderer>().enabled = false;
            Set(root.GetComponent<CharacterFlash>(), "source", telegraph.GetComponent<MeshFilter>());
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = GameLayers.Enemy;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Output + "/Enemy_WoodlandSentinel.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Soundtrack(EnemySpawner first, EnemySpawner second)
        {
            var host = new GameObject("Exploration and Combat Music");
            host.transform.SetParent(dressing);
            var music = host.AddComponent<EncounterMusic>();
            SetArray(music, "encounters", new Object[] { first, second });
            Set(music, "exploration", Audio("Exploration", "Assets/Audio/25 Rpg Game Tracks/Light Ambient 3 (Loop).wav", Vector3.zero, 0.38f, false));
            Set(music, "combat", Audio("Combat", "Assets/Audio/25 Rpg Game Tracks/Action 2 (Loop).wav", Vector3.zero, 0f, false));
            SetArray(music, "combatTracks", new Object[] {
                Require<AudioClip>("Assets/Audio/25 Rpg Game Tracks/Action 1 (Loop).wav"),
                Require<AudioClip>("Assets/Audio/25 Rpg Game Tracks/Action 2 (Loop).wav"),
                Require<AudioClip>("Assets/Audio/25 Rpg Game Tracks/Action 3 (Loop).wav"),
                Require<AudioClip>("Assets/Audio/25 Rpg Game Tracks/Action 4 (Loop).wav"),
                Require<AudioClip>("Assets/Audio/25 Rpg Game Tracks/Action 5 (Loop).wav") });
            Set(music, "forest", Audio("Forest Birds", "Assets/Audio/Nature - Essentials/Ambiance_Forest_Birds_Loop_Stereo.wav", Vector3.zero, 0.2f, false));
            Audio("Forest Wind", "Assets/Audio/Nature - Essentials/Ambiance_Wind_Forest_Loop_Stereo.wav", Vector3.zero, 0.10f, false);
            Audio("Campfire", "Assets/Audio/Nature - Essentials/Ambiance_Firecamp_Small_Loop_Mono.wav", new Vector3(-4.5f, 0.5f, -29f), 0.6f, true);
        }

        private static AudioSource Audio(string name, string path, Vector3 position, float volume, bool spatial)
        {
            AudioClip clip = Require<AudioClip>(path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.65f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(dressing);
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 15f;
            source.dopplerLevel = 0f;
            return source;
        }

        private static void Lighting()
        {
            var sun = GameObject.Find("Directional Light").GetComponent<Light>();
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            sun.color = new Color(1f, 0.87f, 0.67f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.skybox = Require<Material>("Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.49f, 0.62f, 0.73f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.47f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.26f, 0.19f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.58f, 0.68f, 0.66f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 110f;
            Camera.main.farClipPlane = 150f;
        }

        private static void Fire(Vector3 position)
        {
            var flame = new GameObject("Campfire Embers").AddComponent<ParticleSystem>();
            flame.transform.SetParent(dressing);
            flame.transform.position = position;
            var main = flame.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.6f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.35f, 0.04f);
            main.maxParticles = 24;
            var emission = flame.emission;
            emission.rateOverTime = 18f;
            var shape = flame.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.18f;
            flame.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            flame.GetComponent<ParticleSystemRenderer>().sharedMaterial = Solid("Embers", new Color(1f, 0.3f, 0.03f));
            var light = new GameObject("Campfire Glow").AddComponent<Light>();
            light.transform.SetParent(dressing);
            light.transform.position = position + Vector3.up * 0.4f;
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.4f, 0.12f);
            light.shadows = LightShadows.None;
        }

        private static GameObject Prop(string path, Vector3 position, float height, float yaw, bool collision, bool tree = false)
        {
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(path));
            PrefabUtility.UnpackPrefabInstance(prop, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            prop.transform.SetParent(dressing, false);
            Normalize(prop, height);
            prop.transform.position += position;
            prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ConvertMaterials(prop);
            foreach (Collider old in prop.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(old);
            if (collision)
            {
                var bounds = BoundsOf(prop);
                if (tree)
                {
                    var capsule = prop.AddComponent<CapsuleCollider>();
                    capsule.center = prop.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + height * 0.3f, bounds.center.z));
                    capsule.height = height * 0.6f / prop.transform.lossyScale.y;
                    capsule.radius = 0.28f / prop.transform.lossyScale.x;
                }
                else
                {
                    var box = prop.AddComponent<BoxCollider>();
                    box.center = prop.transform.InverseTransformPoint(bounds.center);
                    box.size = new Vector3(bounds.size.x / prop.transform.lossyScale.x, bounds.size.y / prop.transform.lossyScale.y, bounds.size.z / prop.transform.lossyScale.z) * 0.85f;
                }
            }
            foreach (Transform child in prop.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = GameLayers.Default;
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
            }
            return prop;
        }

        private static Bounds BoundsOf(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(obj.name + " has no renderers.");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void Normalize(GameObject obj, float height)
        {
            obj.transform.localPosition = Vector3.zero;
            Bounds bounds = BoundsOf(obj);
            obj.transform.localScale *= height / Mathf.Max(0.01f, bounds.size.y);
            bounds = BoundsOf(obj);
            obj.transform.position -= new Vector3(bounds.center.x - obj.transform.position.x, bounds.min.y - obj.transform.position.y, bounds.center.z - obj.transform.position.z);
        }

        private static void ConvertMaterials(GameObject obj)
        {
            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    if (!converted.TryGetValue(source, out Material target))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                        string path = Output + "/Materials/" + source.name.Replace('/', '_') + "_" + guid + ".mat";
                        target = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (target == null)
                        {
                            target = new Material(Shader.Find("Standard"));
                            AssetDatabase.CreateAsset(target, path);
                        }
                        Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") :
                            source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") :
                            source.HasProperty("_BaseTexture") ? source.GetTexture("_BaseTexture") : null;
                        Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.color : Color.white;
                        if (!source.shader.name.StartsWith("Universal Render Pipeline/") && source.shader.isSupported)
                        {
                            target.shader = source.shader;
                            target.CopyPropertiesFromMaterial(source);
                            if (target.HasProperty("_WindStrength")) target.SetFloat("_WindStrength", 0.15f);
                            if (target.HasProperty("_WindStrength1")) target.SetFloat("_WindStrength1", 0.1f);
                        }
                        else
                        {
                            target.shader = Shader.Find("Standard");
                            target.SetTexture("_MainTex", texture);
                            target.SetColor("_Color", color);
                        }
                        if (target.HasProperty("_Smoothness")) target.SetFloat("_Smoothness", 0.12f);
                        if (target.HasProperty("_Glossiness")) target.SetFloat("_Glossiness", 0.12f);
                        target.enableInstancing = true;
                        EditorUtility.SetDirty(target);
                        converted[source] = target;
                    }
                    materials[i] = target;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material Solid(string name, Color color)
        {
            string path = Output + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Standard");
            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", 0.1f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(dressing);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(obj.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic);
            return obj;
        }

        private static void Boundary(Vector3 position, Vector3 size)
        {
            var obj = new GameObject("World Boundary");
            obj.transform.SetParent(dressing);
            obj.transform.position = position;
            obj.AddComponent<BoxCollider>().size = size;
        }

        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);
        private static void Set(Object target, string name, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray(Object target, string name, Object[] values)
        {
            var data = new SerializedObject(target);
            var property = data.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void RenderPreview()
        {
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(27f, 25f, -43f);
            camera.transform.LookAt(new Vector3(0f, 3f, 3f));
            camera.fieldOfView = 57f;
            camera.farClipPlane = 180f;
            var texture = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var capture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            capture.Apply();
            Directory.CreateDirectory("ArtSource/WorldPreviews");
            File.WriteAllBytes("ArtSource/WorldPreviews/WatchtowerWoodland.png", capture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
