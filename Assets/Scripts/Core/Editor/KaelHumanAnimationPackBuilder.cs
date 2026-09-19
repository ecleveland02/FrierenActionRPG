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
            KaelMovementSetup.Build();
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
            KaelMovementSetup.Build();
        }

        static void AddTurnState(AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip clip, string trigger)
        {
            if (clip == null) return;
            var state = machine.AddState(trigger);
            state.motion = clip;
            var enter = locomotion.AddTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.08f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            var exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.1f;
        }

        static void NormalizeVisualScale(GameObject visual, CharacterController controller)
        {
            if (controller == null) return;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.001f) return;
            float targetHeight = controller.height;
            visual.transform.localScale *= targetHeight / bounds.size.y;
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
                string normalizedFile = Normalize(file);
                string normalizedClip = Normalize(clipName);
                if (!normalizedFile.Contains(normalizedClip) &&
                    !(normalizedClip.EndsWith("backwards") && normalizedFile.Contains("backward")))
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

        static string Normalize(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }
    }
}
