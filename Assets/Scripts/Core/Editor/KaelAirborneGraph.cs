using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Frieren.Core.EditorTools
{
    public static class KaelAirborneGraph
    {
        public static void Build(AnimatorController controller, AnimatorState ground,
            AnimationClip takeoff, AnimationClip falling, AnimationClip landing, AnimationClip[] dodges)
        {
            foreach (var name in new[] { "HardLanding", "IsDodging" })
                controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (var name in new[] { "DodgeX", "DodgeY", "DodgePlayback" })
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var rise = machine.AddState("Jump Takeoff"); rise.motion = takeoff;
            var fall = machine.AddState("Falling"); fall.motion = falling;
            var soft = machine.AddState("Soft Landing"); soft.motion = landing; soft.speed = 1.3f;
            var hard = machine.AddState("Heavy Landing"); hard.motion = landing; hard.speed = 0.85f;
            soft.iKOnFeet = hard.iKOnFeet = true;
            foreach (var state in new[] { ground, soft, hard })
            {
                var up = To(state, rise);
                up.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
                up.AddCondition(AnimatorConditionMode.Greater, 0f, "VerticalVelocity");
                var down = To(state, fall);
                down.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
                down.AddCondition(AnimatorConditionMode.Less, 0.001f, "VerticalVelocity");
            }
            var apex = To(rise, fall);
            apex.AddCondition(AnimatorConditionMode.Less, 0.001f, "VerticalVelocity");
            apex.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            foreach (var air in new[] { rise, fall })
            {
                var light = To(air, soft);
                light.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                light.AddCondition(AnimatorConditionMode.IfNot, 0f, "HardLanding");
                var heavy = To(air, hard);
                heavy.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                heavy.AddCondition(AnimatorConditionMode.If, 0f, "HardLanding");
            }
            foreach (var state in new[] { soft, hard })
            {
                var move = To(state, ground);
                move.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
                move.hasExitTime = true; move.exitTime = 0.12f;
                var recover = To(state, ground);
                recover.hasExitTime = true; recover.exitTime = 0.85f;
            }
            var evade = machine.AddState("Directional Dodge");
            var tree = new BlendTree { name = "Directional Evasion", blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "DodgeX", blendParameterY = "DodgeY" };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(dodges[0], Vector2.up); tree.AddChild(dodges[1], Vector2.down);
            tree.AddChild(dodges[2], Vector2.left); tree.AddChild(dodges[3], Vector2.right);
            evade.motion = tree; evade.speedParameterActive = true; evade.speedParameter = "DodgePlayback";
            var enter = machine.AddAnyStateTransition(evade);
            enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "IsDodging");
            var exit = To(evade, ground);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDodging");
            exit.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            var airborneExit = To(evade, fall);
            airborneExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDodging");
            airborneExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        }

        private static AnimatorStateTransition To(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.08f;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            return transition;
        }
    }
}
