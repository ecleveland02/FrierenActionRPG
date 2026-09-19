using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Frieren.Core.EditorTools
{
    /// <summary>Editable, in-place humanoid sidestep placeholders, not motion-captured rolls.</summary>
    public static class KaelEvasionClips
    {
        public static AnimationClip[] Build(Animator source, AnimationClip idle)
        {
            var copy = Object.Instantiate(source.gameObject);
            var graph = PlayableGraph.Create("Evasion reference pose");
            HumanPose pose = new HumanPose();
            try
            {
                var animator = copy.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, idle);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play(); graph.Evaluate(0.01f);
                using (var handler = new HumanPoseHandler(animator.avatar, animator.transform)) handler.GetHumanPose(ref pose);
            }
            finally { graph.Destroy(); Object.DestroyImmediate(copy); }
            string[] names = { "Forward", "Backward", "Left", "Right" };
            var result = new AnimationClip[4];
            for (int direction = 0; direction < 4; direction++)
            {
                var clip = new AnimationClip { name = "Kael Evade " + names[direction], frameRate = 60f };
                var peak = (float[])pose.muscles.Clone();
                Add(peak, "Spine Front-Back", direction == 1 ? -0.12f : 0.3f);
                Add(peak, "Spine Left-Right", direction == 2 ? -0.25f : direction == 3 ? 0.25f : 0f);
                Add(peak, "Left Upper Leg Front-Back", direction == 1 ? 0.15f : 0.4f);
                Add(peak, "Right Upper Leg Front-Back", direction == 1 ? 0.4f : 0.15f);
                Add(peak, "Left Lower Leg Stretch", -0.65f);
                Add(peak, "Right Lower Leg Stretch", -0.55f);
                Add(peak, "Left Upper Leg In-Out", direction == 2 ? 0.3f : 0.08f);
                Add(peak, "Right Upper Leg In-Out", direction == 3 ? 0.3f : 0.08f);
                Add(peak, "Left Arm Front-Back", 0.2f);
                Add(peak, "Right Arm Front-Back", -0.15f);
                for (int muscle = 0; muscle < HumanTrait.MuscleCount; muscle++)
                    clip.SetCurve("", typeof(Animator), HumanTrait.MuscleName[muscle], new AnimationCurve(
                        new Keyframe(0f, pose.muscles[muscle]), new Keyframe(0.1f, peak[muscle]),
                        new Keyframe(0.25f, peak[muscle]), new Keyframe(0.45f, pose.muscles[muscle])));
                string path = "Assets/Art/Characters/Kael/" + clip.name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) { AssetDatabase.CreateAsset(clip, path); result[direction] = clip; }
                else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); EditorUtility.SetDirty(existing); result[direction] = existing; }
            }
            return result;
        }

        private static void Add(float[] muscles, string name, float amount)
        {
            int index = Array.IndexOf(HumanTrait.MuscleName, name);
            if (index < 0) throw new InvalidOperationException("Unknown Humanoid muscle: " + name);
            muscles[index] = Mathf.Clamp(muscles[index] + amount, -1f, 1f);
        }
    }
}
