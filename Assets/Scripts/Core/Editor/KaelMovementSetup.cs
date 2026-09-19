using System;
using System.IO;
using System.Linq;
using System.Text;
using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    /// <summary>Configures the existing player visual, never guesses a replacement model.</summary>
    public static class KaelMovementSetup
    {
        const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
        const string ClipFolder = "Assets/Animations/KaelAnimations - Copy/";
        const string ControllerPath = "Assets/Art/Characters/Kael/KaelMovement.controller";
        [MenuItem("Frieren/Kael/Set Up Responsive Movement", priority = 59)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before configuring movement.");
            var report = new StringBuilder();
            string[] names = { "Breathing Idle", "Walking", "Running", "Sprint",
                "Walking Backwards", "Running Backward", "Jog Strafe Left", "Jog Strafe Right" };
            var clips = names.Select(n => ImportClip(n, report)).ToArray();
            var leftTurn = ImportClip("Left Turn", report);
            var rightTurn = ImportClip("Right Turn", report);
            const string jumpFolder = "Assets/Animations/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/";
            var takeoff = ImportClip("Kael Takeoff", report, jumpFolder + "HumanM@Jump01 - Begin.fbx", false);
            var fall = ImportClip("Kael Fall", report, jumpFolder + "HumanM@Fall01.fbx");
            var land = ImportClip("Kael Land", report, jumpFolder + "HumanM@Jump01 - Land.fbx", false);
            var prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var adapter = prefab.GetComponent<MecanimCharacterAnimation>();
                if (adapter == null) throw new InvalidOperationException("Player needs MecanimCharacterAnimation.");
                var adapterData = new SerializedObject(adapter);
                var animator = adapterData.FindProperty("animator").objectReferenceValue as Animator;
                if (animator == null) animator = prefab.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("The current Player visual needs a valid Humanoid Animator/Avatar. No model was replaced.");
                if (animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                    throw new InvalidOperationException("Player Animator has no skinned character mesh.");

                // Sample imported clips on a disposable copy before changing the working prefab.
                foreach (var clip in clips) ValidateMotion(animator, clip, report);
                foreach (var clip in new[] { takeoff, fall, land }) ValidateMotion(animator, clip, report);
                var dodges = KaelEvasionClips.Build(animator, clips[0]);
                foreach (var clip in dodges) ValidateMotion(animator, clip, report);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                else
                {
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                        if (asset != controller) Object.DestroyImmediate(asset, true);
                    controller.layers = Array.Empty<AnimatorControllerLayer>();
                    controller.parameters = Array.Empty<AnimatorControllerParameter>();
                    controller.AddLayer("Base Layer");
                }
                foreach (string parameter in new[] { "Speed", "MoveBlend", "MoveDirection", "MoveForward", "VerticalVelocity", "Turn" })
                    controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
                controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
                controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
                var layers = controller.layers;
                layers[0].defaultWeight = 1f;
                controller.layers = layers;
                var machine = controller.layers[0].stateMachine;
                var locomotion = machine.AddState("Ground Locomotion");
                var tree = new BlendTree { name = "Idle Walk Run Sprint", blendType = BlendTreeType.FreeformCartesian2D,
                    blendParameter = "MoveDirection", blendParameterY = "MoveForward", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(tree, controller);
                var idleTree = new BlendTree { name = "Idle and Responsive Turns", blendType = BlendTreeType.Simple1D,
                    blendParameter = "Turn", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(idleTree, controller);
                idleTree.AddChild(leftTurn, -1f);
                idleTree.AddChild(clips[0], 0f);
                idleTree.AddChild(rightTurn, 1f);
                tree.AddChild(idleTree, Vector2.zero);
                tree.AddChild(clips[1], new Vector2(0f, 2f / 7f));
                tree.AddChild(clips[2], new Vector2(0f, 4.5f / 7f));
                tree.AddChild(clips[3], Vector2.up);
                tree.AddChild(clips[4], new Vector2(0f, -2f / 7f));
                tree.AddChild(clips[5], new Vector2(0f, -4.5f / 7f));
                tree.AddChild(clips[6], new Vector2(-4.5f / 7f, 0f));
                tree.AddChild(clips[7], new Vector2(4.5f / 7f, 0f));
                locomotion.motion = tree;
                locomotion.iKOnFeet = true;
                machine.defaultState = locomotion;
                KaelAirborneGraph.Build(controller, locomotion, takeoff, fall, land, dodges);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.enabled = true;
                animator.speed = 1f;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                adapterData.FindProperty("animator").objectReferenceValue = animator;
                adapterData.FindProperty("blendDampTime").floatValue = 0.08f;
                adapterData.ApplyModifiedPropertiesWithoutUndo();
                adapter.enabled = true;
                var placeholder = prefab.GetComponent<PlaceholderCharacterAnimation>();
                if (placeholder != null) Object.DestroyImmediate(placeholder);
                if (prefab.GetComponent<CharacterStamina>() == null) prefab.AddComponent<CharacterStamina>();
                var dodge = prefab.GetComponent<PlayerDodge>();
                if (dodge == null) throw new InvalidOperationException("Player has no PlayerDodge.");
                var dodgeData = new SerializedObject(dodge);
                SetFloat(dodgeData, "duration", 0.45f);
                SetFloat(dodgeData, "cooldown", 0.2f);
                SetFloat(dodgeData, "startSpeed", 12f);
                SetFloat(dodgeData, "staminaCost", 25f);
                SetFloat(dodgeData, "invulnerabilityStart", 0.06f);
                SetFloat(dodgeData, "invulnerabilityDuration", 0.2f);
                dodgeData.ApplyModifiedPropertiesWithoutUndo();
                var movement = prefab.GetComponent<PlayerLocomotion>();
                if (movement == null) throw new InvalidOperationException("Player has no PlayerLocomotion.");
                var movementData = new SerializedObject(movement);
                SetFloat(movementData, "walkSpeed", 2f);
                SetFloat(movementData, "runSpeed", 4.5f);
                SetFloat(movementData, "sprintSpeed", 7f);
                SetFloat(movementData, "turnSpeed", 1080f);
                SetFloat(movementData, "acceleration", 30f);
                SetFloat(movementData, "deceleration", 40f);
                movementData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                report.AppendLine("PASS: Player's existing Humanoid visual assigned KaelMovement.controller.");
                report.AppendLine("Walk 2 m/s, run 4.5 m/s, sprint 7 m/s; turn 1080 degrees/s.");
                File.WriteAllText("Library/KaelMovement-result.txt", report.ToString());
                Debug.Log(report.ToString());
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void SetFloat(SerializedObject obj, string name, float value) => obj.FindProperty(name).floatValue = value;

        static AnimationClip ImportClip(string name, StringBuilder report, string exactPath = null, bool loop = true)
        {
            // Exact filenames distinguish Running from Running Backward and Walking from Stop Walking.
            string path = exactPath ?? ClipFolder + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing FBX importer: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            var settings = importer.defaultClipAnimations;
            if (settings.Length != 1) throw new InvalidOperationException(path + " must expose one animation take; found " + settings.Length);
            settings[0].name = name;
            settings[0].loopTime = loop;
            settings[0].loopPose = loop;
            // Extract displacement instead of baking travel into the skeleton. The motor moves the root.
            settings[0].lockRootPositionXZ = false;
            settings[0].lockRootRotation = false;
            settings[0].keepOriginalOrientation = true;
            settings[0].lockRootHeightY = true;
            settings[0].heightFromFeet = true;
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().SingleOrDefault(c => c.name == name);
            if (clip == null || !clip.humanMotion || clip.length <= 0f)
                throw new InvalidOperationException("No playable Humanoid clip in " + path + ". Check FBX import errors in Console.");
            report.AppendLine(name + ": Humanoid, loop=" + loop + ", " + clip.length.ToString("0.00") + " seconds.");
            return clip;
        }

        static void ValidateMotion(Animator source, AnimationClip clip, StringBuilder report)
        {
            var copy = Object.Instantiate(source.gameObject);
            var graph = PlayableGraph.Create("Kael retarget verification");
            try
            {
                var animator = copy.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                var output = AnimationPlayableOutput.Create(graph, "Kael", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                var bones = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Spine, HumanBodyBones.LeftHand };
                graph.Evaluate(0f);
                var rotations = bones.Select(b => animator.GetBoneTransform(b).localRotation).ToArray();
                float motion = 0f;
                foreach (float phase in new[] { 0.23f, 0.47f, 0.71f })
                {
                    playable.SetTime(clip.length * phase);
                    graph.Evaluate(0f);
                    for (int i = 0; i < bones.Length; i++)
                        motion += Quaternion.Angle(rotations[i], animator.GetBoneTransform(bones[i]).localRotation);
                }
                if (motion < 0.01f) throw new InvalidOperationException(clip.name + " did not animate the current Kael bones during sampling.");
                report.AppendLine(clip.name + " retarget check: " + motion.ToString("0.0") + " degrees of sampled bone movement.");
            }
            finally { graph.Destroy(); Object.DestroyImmediate(copy); }
        }
    }
}
