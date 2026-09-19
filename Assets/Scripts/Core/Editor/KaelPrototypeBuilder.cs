using System;
using System.IO;
using System.Linq;
using Frieren.Characters.Animation;
using Frieren.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    /// <summary>Imports the Blender prototype and creates a separate playable Kael prefab.</summary>
    public static class KaelPrototypeBuilder
    {
        public const string ArtRoot = "Assets/Art/Characters/Kael";
        public const string ModelFolder = ArtRoot + "/Models/";
        public const string PrefabPath = "Assets/Prefabs/Characters/KaelPlayer.prefab";
        public const string PreviewScene = "Assets/Scenes/Archive/KaelTestScene.unity";
        private const string ControllerPath = ArtRoot + "/Kael.controller";
        private static readonly string[] ClipNames = { "Idle", "Walk", "Run", "Air", "Land", "Dodge", "Cast", "Hit" };

        [MenuItem("Frieren/Kael/Build Character Prototype", priority = 60)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before building the Kael assets.");

            // Created outputs are artist-owned once built. Rebuild into a new version instead of
            // silently replacing a controller or prefab someone has begun editing.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene) != null)
                throw new InvalidOperationException("Kael outputs already exist. Keep them or move them to an archive folder before rebuilding.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "Kael.fbx");
            if (model == null)
                throw new InvalidOperationException("Run Tools/Blender/build_kael.py in Blender first.");

            var clips = ClipNames.ToDictionary(name => name, LoadClip);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PlayerPrefab);
            if (source == null)
                throw new InvalidOperationException("The working Player prefab must exist first.");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveBlend", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            foreach (string trigger in new[] { "Jump", "Land", "Dodge", "CastStart", "CastRelease", "Hit" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion", new Vector3(260, 80));
            var blend = new BlendTree { name = "Kael Locomotion", blendType = BlendTreeType.Simple1D,
                blendParameter = "MoveBlend", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(clips["Idle"], 0f);
            // PlayerLocomotion defines MoveBlend as speed / sprintSpeed: 4.5 / 8 = 0.5625.
            blend.AddChild(clips["Walk"], 0.5625f);
            blend.AddChild(clips["Run"], 1f);
            locomotion.motion = blend;
            machine.defaultState = locomotion;

            var air = machine.AddState("Air", new Vector3(260, 220));
            air.motion = clips["Air"];
            var leaveGround = locomotion.AddTransition(air);
            leaveGround.hasExitTime = false;
            leaveGround.duration = .08f;
            leaveGround.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            var land = machine.AddState("Land", new Vector3(500, 220));
            land.motion = clips["Land"];
            var touchGround = air.AddTransition(land);
            touchGround.hasExitTime = false;
            touchGround.duration = .05f;
            touchGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            ReturnToLocomotion(land, locomotion);
            AddAction(machine, locomotion, clips["Dodge"], "Dodge", 360);
            AddAction(machine, locomotion, clips["Cast"], "CastStart", 440);
            AddAction(machine, locomotion, clips["Hit"], "Hit", 520);

            GameObject root = Object.Instantiate(source);
            root.name = "KaelPlayer";
            try
            {
                var placeholder = root.GetComponent<PlaceholderCharacterAnimation>();
                if (placeholder != null) Object.DestroyImmediate(placeholder);
                Transform oldVisual = root.transform.Find("Visual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "KaelVisual";
                visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var adapter = root.GetComponent<MecanimCharacterAnimation>();
                if (adapter == null) adapter = root.AddComponent<MecanimCharacterAnimation>();
                var serialized = new SerializedObject(adapter);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = root.layer;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }

            if (!AssetDatabase.CopyAsset(ProjectPaths.TestScene, PreviewScene))
                throw new InvalidOperationException("Could not copy TestScene for the Kael test scene.");
            Scene activeBefore = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Additive);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                int count = 0;
                foreach (var sceneRoot in preview.GetRootGameObjects())
                foreach (var spawner in sceneRoot.GetComponentsInChildren<PlayerSpawner>(true))
                {
                    var serialized = new SerializedObject(spawner);
                    serialized.FindProperty("playerPrefab").objectReferenceValue = prefab;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
                if (count == 0) throw new InvalidOperationException("TestScene contains no PlayerSpawner.");
                EditorSceneManager.SaveScene(preview);
            }
            finally
            {
                EditorSceneManager.CloseScene(preview, true);
                if (activeBefore.IsValid() && activeBefore.isLoaded) SceneManager.SetActiveScene(activeBefore);
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("[Kael] Built 8 imported clips, controller, KaelPlayer and KaelTestScene. " +
                      "Use Frieren > Kael > Open Character Test Scene. CastStart can be previewed in the Animator; casting gameplay is not added.");
        }

        [MenuItem("Frieren/Kael/Validate Character Assets", priority = 62)]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Kael prefab is missing.");
            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || animator.applyRootMotion)
                    throw new InvalidOperationException("Kael needs a valid avatar and disabled root motion.");
                if (instance.GetComponent<PlaceholderCharacterAnimation>() != null ||
                    instance.GetComponent<MecanimCharacterAnimation>() == null)
                    throw new InvalidOperationException("Kael animation adapter is not configured.");
                if (instance.GetComponentsInChildren<Collider>().Length != 1)
                    throw new InvalidOperationException("Only the original CharacterController should collide.");
                foreach (string name in ClipNames)
                {
                    var clip = LoadClip(name);
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    if (bindings.Length == 0) throw new InvalidOperationException(name + " has no animation curves.");
                    foreach (var binding in bindings)
                        if (binding.type == typeof(Transform) && binding.path.Length > 0 &&
                            animator.transform.Find(binding.path) == null)
                            throw new InvalidOperationException(name + " references missing bone " + binding.path);
                    foreach (float phase in new[] { 0f, .25f, .5f, .75f, .999f })
                    {
                        clip.SampleAnimation(animator.gameObject, clip.length * phase);
                        foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var baked = new Mesh();
                            try
                            {
                                skin.BakeMesh(baked);
                                if (baked.vertexCount == 0 || baked.bounds.size.magnitude > 5f ||
                                    float.IsNaN(baked.bounds.size.magnitude))
                                    throw new InvalidOperationException(name + " has invalid skinning or scale.");
                            }
                            finally { Object.DestroyImmediate(baked); }
                        }
                    }
                    Debug.Log($"[Kael Validate] {name}: {clip.length:0.000}s, {bindings.Length} curves, 5 sampled poses passed.");
                }
                Debug.Log("[Kael Validate] PASS: avatar, adapter, collider count, clip bindings and 40 sampled poses.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // Batch verification entry point. Run without -nographics to inspect the actual Unity import.
        public static void CapturePreview()
        {
            Validate();
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var cameraObject = new GameObject("Kael verification camera");
            var lightObject = new GameObject("Kael verification light");
            var fillObject = new GameObject("Kael verification fill");
            var target = new RenderTexture(900, 1000, 24);
            var image = new Texture2D(900, 1000, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var animator = instance.GetComponentInChildren<Animator>();
                LoadClip("Idle").SampleAnimation(animator.gameObject, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(2.5f, 1.9f, 5f);
                camera.transform.LookAt(new Vector3(0, .95f, 0));
                camera.orthographic = true;
                camera.orthographicSize = 1.12f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.10f, .14f, .19f);
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(35, -130, 0);
                var fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = .5f;
                fill.cullingMask = 1 << 31;
                fill.transform.rotation = Quaternion.Euler(20, 45, 0);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 900, 1000), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/KaelUnity.png", image.EncodeToPNG());
                Debug.Log("[Kael Validate] Unity render written to Logs/KaelUnity.png");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(fillObject);
                Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("Frieren/Kael/Open Character Test Scene", priority = 61)]
        private static void OpenTest()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene) == null)
            {
                Debug.LogError("Build the Kael prototype first.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(PreviewScene);
        }

        private static AnimationClip LoadClip(string name)
        {
            string path = ModelFolder + "Kael_" + name + ".fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null || clip.length <= 0)
                throw new InvalidOperationException("Missing or empty animation: " + path);
            return clip;
        }

        private static void AddAction(AnimatorStateMachine machine, AnimatorState locomotion,
            AnimationClip clip, string trigger, float y)
        {
            var state = machine.AddState(clip.name, new Vector3(500, y));
            state.motion = clip;
            var enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = .06f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
            ReturnToLocomotion(state, locomotion);
        }

        private static void ReturnToLocomotion(AnimatorState state, AnimatorState locomotion)
        {
            var exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = .9f;
            exit.duration = .08f;
        }
    }

    /// <summary>Import rules scoped only to our generated FBXs; Unity writes their metadata.</summary>
    internal sealed class KaelModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(KaelPrototypeBuilder.ModelFolder, StringComparison.Ordinal)) return;
            // KaelRigged contains artist-supplied Humanoid exports. Do not overwrite their
            // importer choice with the Generic settings used by the legacy prototype FBXs.
            if (assetPath.StartsWith(KaelPrototypeBuilder.ModelFolder + "KaelRigged/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importAnimation = Path.GetFileNameWithoutExtension(assetPath) != "Kael";
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(KaelPrototypeBuilder.ModelFolder, StringComparison.Ordinal)) return;
            if (assetPath.StartsWith(KaelPrototypeBuilder.ModelFolder + "KaelRigged/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            string clipName = Path.GetFileNameWithoutExtension(assetPath).Replace("Kael_", "");
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clipName;
                clip.loopTime = clipName == "Idle" || clipName == "Walk" || clipName == "Run" || clipName == "Air";
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
