using System;
using System.IO;
using System.Linq;
using Frieren.Characters.Animation;
using Frieren.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    /// <summary>Explicit, non-overwriting setup for the separate Meshy cloth experiment.</summary>
    public static class KaelClothBuilder
    {
        private const string Folder = "Assets/Art/Characters/KaelMeshyCloth";
        private const string ModelPath = Folder + "/KaelCloth.fbx";
        private const string PrefabPath = "Assets/Prefabs/Characters/KaelClothPlayer.prefab";
        private const string ScenePath = "Assets/Scenes/Archive/KaelClothTestScene.unity";
        private const string MaterialPath = Folder + "/KaelCloth.mat";
        private const string TextureRoot = "Assets/Art/Characters/KaelMeshy/Meshy_AI_Create_a_game_ready_3_biped_texture_0";

        [MenuItem("Frieren/Kael/Build Meshy Cloth Test", priority = 70)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before building cloth assets.");
            foreach (string path in new[] { PrefabPath, ScenePath, MaterialPath })
                if (File.Exists(path))
                    throw new InvalidOperationException("Output already exists; move it aside in Unity before rebuilding: " + path);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PlayerPrefab);
            if (source == null) throw new InvalidOperationException("The working Player prefab is missing.");
            ConfigureImport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("This setup targets the project's Built-in render pipeline.");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + ".png");
            if (albedo == null) throw new InvalidOperationException("Meshy albedo texture is missing.");
            var material = new Material(shader) { name = "Kael Cloth", mainTexture = albedo };
            material.SetFloat("_Glossiness", .2f);
            var normalImporter = AssetImporter.GetAtPath(TextureRoot + "_normal.png") as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "_normal.png"));
                material.EnableKeyword("_NORMALMAP");
            }

            GameObject root = Object.Instantiate(source);
            root.name = "KaelClothPlayer";
            try
            {
                var placeholder = root.GetComponent<PlaceholderCharacterAnimation>();
                if (placeholder != null) Object.DestroyImmediate(placeholder);
                var adapter = root.GetComponent<MecanimCharacterAnimation>();
                if (adapter != null) Object.DestroyImmediate(adapter);
                Transform oldVisual = root.transform.Find("Visual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "KaelClothVisual";
                visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException("The cloth model needs a valid Humanoid avatar. Inspect its Rig configuration.");
                animator.applyRootMotion = false;
                // Keep the bind pose until real Humanoid clips are assigned; no old Generic controller.
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMaterial = material;
                    renderer.updateWhenOffscreen = true;
                    Bounds bounds = renderer.localBounds;
                    bounds.Expand(.8f);
                    renderer.localBounds = bounds;
                }
                var skin = visual.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(r => r.name == "Kael_CoatCloth");
                var cloth = ConfigureConstraints(skin);
                cloth.useGravity = true;
                cloth.stretchingStiffness = .95f;
                cloth.bendingStiffness = .65f;
                cloth.damping = .4f;
                cloth.useTethers = true;
                cloth.worldVelocityScale = .25f;
                cloth.worldAccelerationScale = .15f;
                cloth.clothSolverFrequency = 120f;
                cloth.friction = .4f;
                cloth.enableContinuousCollision = true;
                cloth.useVirtualParticles = 1f;
                cloth.capsuleColliders = new[]
                {
                    AddCapsule(visual.transform, "LeftUpLeg", "LeftLeg", .065f),
                    AddCapsule(visual.transform, "LeftLeg", "LeftFoot", .055f),
                    AddCapsule(visual.transform, "RightUpLeg", "RightLeg", .065f),
                    AddCapsule(visual.transform, "RightLeg", "RightFoot", .055f),
                    AddCapsule(visual.transform, "Hips", "Spine02", .10f)
                };
                skin.gameObject.AddComponent<CharacterClothWind>();
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
                AssetDatabase.CreateAsset(material, MaterialPath);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save cloth prefab.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (!AssetDatabase.Contains(material)) Object.DestroyImmediate(material);
            }
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kael Cloth] Created a separate cloth player and test scene. No animations are assigned yet. " +
                      "Open Frieren > Kael > Open Meshy Cloth Test, press Play and test wind/movement. " +
                      "World objects need explicit capsule/sphere entries on the Cloth component.");
        }

        private static void ConfigureImport()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Run Tools/Blender/prepare_kael_cloth.py first.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var mappings = new[]
            {
                ("Hips", "Hips"), ("Spine", "Spine02"), ("Chest", "Spine01"), ("UpperChest", "Spine"),
                ("Neck", "neck"), ("Head", "Head"),
                ("LeftShoulder", "LeftShoulder"), ("LeftUpperArm", "LeftArm"),
                ("LeftLowerArm", "LeftForeArm"), ("LeftHand", "LeftHand"),
                ("RightShoulder", "RightShoulder"), ("RightUpperArm", "RightArm"),
                ("RightLowerArm", "RightForeArm"), ("RightHand", "RightHand"),
                ("LeftUpperLeg", "LeftUpLeg"), ("LeftLowerLeg", "LeftLeg"),
                ("LeftFoot", "LeftFoot"), ("LeftToes", "LeftToeBase"),
                ("RightUpperLeg", "RightUpLeg"), ("RightLowerLeg", "RightLeg"),
                ("RightFoot", "RightFoot"), ("RightToes", "RightToeBase")
            };
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = new HumanDescription
            {
                human = mappings.Select(pair => new HumanBone { humanName = pair.Item1, boneName = pair.Item2,
                    limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = model.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                armStretch = .05f, legStretch = .05f,
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f
            };
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.optimizeGameObjects = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
        }

        private static Cloth ConfigureConstraints(SkinnedMeshRenderer skin)
        {
            // Cloth welds UV-seam duplicates, so its particle indices need not match mesh vertices.
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, false);
                Vector3[] meshVertices = baked.vertices;
                for (int j = 0; j < meshVertices.Length; j++)
                    meshVertices[j] = skin.transform.TransformPoint(meshVertices[j]);
                Color[] colors = skin.sharedMesh.colors;
                // Capture the skinned reference before Cloth takes over the renderer.
                var cloth = skin.gameObject.AddComponent<Cloth>();
                Vector3[] particles = cloth.vertices;
                // Cloth particles are relative to the root bone's position/rotation,
                // with scale already applied. BakeMesh returns renderer-local vertices.
                // Compare in world space; rootBone.TransformPoint would apply scale twice.
                Transform clothRoot = skin.rootBone != null ? skin.rootBone : skin.transform;
                for (int i = 0; i < particles.Length; i++)
                    particles[i] = clothRoot.rotation * particles[i] + clothRoot.position;
                var coefficients = cloth.coefficients;
                if (colors.Length != meshVertices.Length || particles.Length != coefficients.Length)
                    throw new InvalidOperationException("Cloth freedom vertex colors or particle data are missing.");
                int pinned = 0, free = 0;
                for (int i = 0; i < particles.Length; i++)
                {
                    float best = float.PositiveInfinity, freedom = 0;
                    for (int j = 0; j < meshVertices.Length; j++)
                    {
                        float distance = (particles[i] - meshVertices[j]).sqrMagnitude;
                        if (distance < best) { best = distance; freedom = colors[j].r; }
                    }
                    if (best > .000001f)
                        throw new InvalidOperationException($"Cloth particle {i} is {Mathf.Sqrt(best):F6}m " +
                            "from the nearest mesh vertex in world space; do not guess particle weights.");
                    coefficients[i].maxDistance = Mathf.Clamp01(freedom) * .35f;
                    coefficients[i].collisionSphereDistance = 0;
                    if (freedom < .001f) pinned++; else free++;
                }
                if (pinned < 10 || free < 100) throw new InvalidOperationException("Cloth needs both pinned and free particles.");
                cloth.coefficients = coefficients;
                Debug.Log($"[Kael Cloth] {particles.Length} particles, {pinned} pinned, {free} free.");
                return cloth;
            }
            finally { Object.DestroyImmediate(baked); }
        }

        [MenuItem("Frieren/Kael/Validate Meshy Cloth Mapping", priority = 72)]
        public static void ValidateMapping()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before validating cloth mapping.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("The cloth model is missing.");
            foreach (bool transformed in new[] { false, true })
            {
                var instance = Object.Instantiate(model);
                try
                {
                    if (transformed)
                    {
                        instance.transform.position = new Vector3(3f, 2f, -4f);
                        instance.transform.rotation = Quaternion.Euler(15f, 67f, 8f);
                        instance.transform.localScale *= 1.25f;
                    }
                    var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Single(r => r.name == "Kael_CoatCloth");
                    ConfigureConstraints(skin);
                }
                finally { Object.DestroyImmediate(instance); }
            }
            Debug.Log("[Kael Cloth] Mapping validation passed at identity and translated/rotated/scaled transforms.");
        }

        private static CapsuleCollider AddCapsule(Transform root, string startName, string endName, float radius)
        {
            Transform start = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == startName);
            Transform end = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == endName);
            var proxy = new GameObject("ClothCollision_" + startName);
            proxy.transform.SetParent(start, false);
            proxy.transform.position = (start.position + end.position) * .5f;
            proxy.transform.rotation = Quaternion.FromToRotation(Vector3.up, end.position - start.position);
            var collider = proxy.AddComponent<CapsuleCollider>();
            float scale = Mathf.Abs(proxy.transform.lossyScale.x);
            collider.radius = radius / scale;
            collider.height = Mathf.Max(radius * 2f, Vector3.Distance(start.position, end.position) * .95f) / scale;
            collider.direction = 1;
            collider.isTrigger = true;
            return collider;
        }

        private static void CreateScene()
        {
            if (!AssetDatabase.CopyAsset(ProjectPaths.TestScene, ScenePath))
                throw new InvalidOperationException("Could not create the separate cloth scene.");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var spawner in root.GetComponentsInChildren<PlayerSpawner>(true))
                {
                    var serialized = new SerializedObject(spawner);
                    serialized.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
                if (count == 0) throw new InvalidOperationException("TestScene has no player spawner.");
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Frieren/Kael/Open Meshy Cloth Test", priority = 71)]
        private static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { Debug.LogError("Build Meshy Cloth Test first."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
