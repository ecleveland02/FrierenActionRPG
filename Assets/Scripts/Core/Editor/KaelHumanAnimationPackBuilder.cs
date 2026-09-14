using System;
using System.IO;
using System.Linq;
using Frieren.Characters.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    public static class KaelHumanAnimationPackBuilder
    {
        const string Controller = "Assets/Art/Characters/Kael/KevinHuman.controller";
        const string Player = "Assets/Prefabs/Characters/Player.prefab";

        [MenuItem("Frieren/Kael/Use Kevin Human Animation Pack", priority = 63)]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AnimationClip Clip(string token) => Find(token);
            var idle = Clip("HumanM@Idle01.fbx");
            var walk = Clip("HumanM@Walk01_Forward.fbx");
            var run = Clip("HumanM@Run01_Forward.fbx");
            var left = Clip("HumanM@Walk01_Left.fbx");
            var right = Clip("HumanM@Walk01_Right.fbx");
            var backward = Clip("HumanM@Walk01_Backward.fbx");
            var jump = Clip("HumanM@Jump01.fbx");
            var land = Clip("HumanM@Jump01 - Land.fbx");
            if (new[] { idle, walk, run, jump, land }.Any(c => c == null))
                throw new InvalidOperationException("Kevin Human animation clips were not imported.");
            AssetDatabase.DeleteAsset(Controller);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveBlend", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveDirection", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveForward", AnimatorControllerParameterType.Float);
            ac.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            ac.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Land", AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            var locomotion = sm.AddState("Locomotion");
            var tree = new BlendTree { name = "Kevin Human Locomotion", blendType = BlendTreeType.FreeformCartesian2D, blendParameter = "MoveDirection", blendParameterY = "MoveForward", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ac);
            tree.AddChild(idle, new Vector2(0f, 0f)); tree.AddChild(walk, new Vector2(0f, 1f));
            if (backward != null) tree.AddChild(backward, new Vector2(0f, -1f));
            if (left != null) tree.AddChild(left, new Vector2(-1f, 0f));
            if (right != null) tree.AddChild(right, new Vector2(1f, 0f));
            locomotion.motion = tree; sm.defaultState = locomotion;
            var sprint = sm.AddState("Sprint"); sprint.motion = run;
            var toSprint = locomotion.AddTransition(sprint); toSprint.hasExitTime = false; toSprint.AddCondition(AnimatorConditionMode.If, 0, "IsSprinting");
            var fromSprint = sprint.AddTransition(locomotion); fromSprint.hasExitTime = false; fromSprint.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
            var air = sm.AddState("Air"); air.motion = jump; var t = locomotion.AddTransition(air); t.hasExitTime = false; t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            var grounded = air.AddTransition(locomotion); grounded.hasExitTime = false; grounded.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            var prefab = PrefabUtility.LoadPrefabContents(Player);
            try { var animator = prefab.GetComponentInChildren<Animator>(true); if (animator == null) throw new InvalidOperationException("Player has no Animator."); animator.runtimeAnimatorController = ac; animator.applyRootMotion = false; PrefabUtility.SaveAsPrefabAsset(prefab, Player); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Kael] Kevin Iglesias Human Animations assigned to Player.prefab.");
        }

        [MenuItem("Frieren/Kael/Build KaelAnimations Copy Controller", priority = 62)]
        public static void BuildKaelAnimationsCopy()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AnimationClip FindClip(string name) => FindInFolder(name, "Assets/Animations/KaelAnimations - Copy");
            var idle = FindClip("Breathing Idle");
            var walk = FindClip("Walking");
            var walkBack = FindClip("Walking Backwards");
            var run = FindClip("Running");
            var runBack = FindClip("Running Backward");
            var sprint = FindClip("Sprint");
            var strafeLeft = FindClip("Jog Strafe Left");
            var strafeRight = FindClip("Jog Strafe Right");
            var jump = FindClip("Jump");
            var land = FindClip("Land");

            if (idle == null || walk == null || walkBack == null || run == null || runBack == null ||
                sprint == null || strafeLeft == null || strafeRight == null)
            {
                throw new InvalidOperationException(
                    "KaelAnimations - Copy must contain Breathing Idle, Walking, Walking Backwards, " +
                    "Running, Running Backward, Sprint, Jog Strafe Left and Jog Strafe Right clips.");
            }

            AssetDatabase.DeleteAsset("Assets/Art/Characters/Kael/Kael.controller");
            var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/Art/Characters/Kael/Kael.controller");
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveBlend", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveDirection", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveForward", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);

            var stateMachine = controller.layers[0].stateMachine;
            var locomotion = stateMachine.AddState("Locomotion");
            var tree = new BlendTree
            {
                name = "Kael Locomotion",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "MoveDirection",
                blendParameterY = "MoveForward",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, new Vector2(0f, 0f));
            tree.AddChild(walk, new Vector2(0f, 0.56f));
            tree.AddChild(walkBack, new Vector2(0f, -0.56f));
            tree.AddChild(strafeLeft, new Vector2(-0.56f, 0f));
            tree.AddChild(strafeRight, new Vector2(0.56f, 0f));
            tree.AddChild(run, new Vector2(0f, 0.82f));
            tree.AddChild(runBack, new Vector2(0f, -0.82f));
            tree.AddChild(sprint, new Vector2(0f, 1f));
            locomotion.motion = tree;
            stateMachine.defaultState = locomotion;

            if (jump != null)
            {
                var air = stateMachine.AddState("Air");
                air.motion = jump;
                var toAir = locomotion.AddTransition(air);
                toAir.hasExitTime = false;
                toAir.duration = 0.12f;
                toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
                var fromAir = air.AddTransition(locomotion);
                fromAir.hasExitTime = false;
                fromAir.duration = 0.16f;
                fromAir.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            }

            if (land != null)
            {
                var landing = stateMachine.AddState("Land");
                landing.motion = land;
                var toLand = locomotion.AddTransition(landing);
                toLand.hasExitTime = false;
                toLand.duration = 0.08f;
                toLand.AddCondition(AnimatorConditionMode.If, 0f, "Land");
                var fromLand = landing.AddTransition(locomotion);
                fromLand.hasExitTime = true;
                fromLand.exitTime = 0.85f;
                fromLand.duration = 0.12f;
            }

            var prefab = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                var animator = prefab.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("Player has no Animator.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(prefab, Player);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Kael] KaelAnimations - Copy locomotion controller assigned to Player.prefab.");
        }

        [MenuItem("Frieren/Kael/Restore Cloth On Player", priority = 64)]
        public static void RestoreCloth()
        {
            var prefab = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                var clothSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/KaelClothPlayer.prefab");
                if (clothSource == null) throw new InvalidOperationException("KaelClothPlayer prefab is missing.");
                var old = prefab.transform.Find("KaelVisual");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(clothSource);
                var visual = instance.transform.Find("KaelClothVisual");
                if (visual == null) throw new InvalidOperationException("Cloth prefab has no KaelClothVisual.");
                visual.SetParent(prefab.transform, false);
                visual.name = "KaelVisual";
                Object.DestroyImmediate(instance);
                var animator = visual.GetComponent<Animator>();
                if (animator != null) animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
                PrefabUtility.SaveAsPrefabAsset(prefab, Player);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Kael] Restored the configured cloth visual on Player.prefab.");
        }

        [MenuItem("Frieren/Kael/Use Imported KaelRigged Model", priority = 65)]
        public static void UseImportedKaelRiggedModel()
        {
            const string modelFolder = "Assets/Art/Characters/Kael/Models/KaelRigged";
            var modelGuid = AssetDatabase.FindAssets("t:Model", new[] { modelFolder })
                .FirstOrDefault(guid =>
                {
                    string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                    return file.IndexOf("KaelRigged", StringComparison.OrdinalIgnoreCase) >= 0 &&
                           file.IndexOf("T-Pose", StringComparison.OrdinalIgnoreCase) >= 0;
                });
            string modelPath = string.IsNullOrEmpty(modelGuid) ? null : AssetDatabase.GUIDToAssetPath(modelGuid);
            var model = string.IsNullOrEmpty(modelPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                throw new InvalidOperationException("KaelRigged T-pose FBX has not finished importing in " + modelFolder);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
            if (controller == null)
                throw new InvalidOperationException("Build the KaelAnimations Copy controller first.");

            var prefab = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                var oldVisual = prefab.transform.Find("KaelVisual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "KaelVisual";
                visual.transform.SetParent(prefab.transform, false);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var adapter = prefab.GetComponent<MecanimCharacterAnimation>();
                if (adapter != null)
                {
                    var serialized = new SerializedObject(adapter);
                    serialized.FindProperty("animator").objectReferenceValue = animator;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = prefab.layer;
                PrefabUtility.SaveAsPrefabAsset(prefab, Player);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Kael] Imported KaelRigged model assigned to Player.prefab.");
        }

        static AnimationClip Find(string file)
        {
            var guid = AssetDatabase.FindAssets(file.Replace(".fbx", ""), new[] { "Assets/Animations" }).FirstOrDefault();
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        static AnimationClip FindInFolder(string clipName, string folder)
        {
            string prefix = folder.TrimEnd('/') + "/";
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf(clipName, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    // FBX exporters commonly name the embedded clip "Take 001" or
                    // "mixamo.com" rather than matching the file name. The file search above
                    // is the identity; only reject Unity's generated preview clip here.
                    if (!clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                        return clip;
                }
            }

            return null;
        }
    }
}
