using System;
using System.Linq;
using Frieren.Characters.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
            var jump = Clip("HumanM@Jump01.fbx");
            var land = Clip("HumanM@Jump01 - Land.fbx");
            if (new[] { idle, walk, run, jump, land }.Any(c => c == null))
                throw new InvalidOperationException("Kevin Human animation clips were not imported.");
            AssetDatabase.DeleteAsset(Controller);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveBlend", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveDirection", AnimatorControllerParameterType.Float);
            ac.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            ac.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Land", AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            var locomotion = sm.AddState("Locomotion");
            var tree = new BlendTree { name = "Kevin Human Locomotion", blendType = BlendTreeType.FreeformCartesian2D, blendParameter = "MoveDirection", blendParameterY = "MoveBlend", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ac);
            tree.AddChild(idle, new Vector2(0f, 0f)); tree.AddChild(walk, new Vector2(0f, .56f)); tree.AddChild(run, new Vector2(0f, 1f));
            if (left != null) tree.AddChild(left, new Vector2(-1f, .56f));
            if (right != null) tree.AddChild(right, new Vector2(1f, .56f));
            locomotion.motion = tree; sm.defaultState = locomotion;
            var air = sm.AddState("Air"); air.motion = jump; var t = locomotion.AddTransition(air); t.hasExitTime = false; t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            var grounded = air.AddTransition(locomotion); grounded.hasExitTime = false; grounded.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            var prefab = PrefabUtility.LoadPrefabContents(Player);
            try { var animator = prefab.GetComponentInChildren<Animator>(true); if (animator == null) throw new InvalidOperationException("Player has no Animator."); animator.runtimeAnimatorController = ac; animator.applyRootMotion = false; PrefabUtility.SaveAsPrefabAsset(prefab, Player); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Kael] Kevin Iglesias Human Animations assigned to Player.prefab.");
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

        static AnimationClip Find(string file)
        {
            var guid = AssetDatabase.FindAssets(file.Replace(".fbx", ""), new[] { "Assets/Animations" }).FirstOrDefault();
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }
    }
}
