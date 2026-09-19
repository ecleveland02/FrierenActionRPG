using Frieren.Characters;
using Frieren.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Frieren.Tests.EditMode
{
    public sealed class KaelMovementTests
    {
        [TestCase(0f, false)]
        [TestCase(0.059f, false)]
        [TestCase(0.06f, true)]
        [TestCase(0.259f, true)]
        [TestCase(0.26f, false)]
        [TestCase(0.45f, false)]
        public void DodgeProtectionOnlyCoversMiddle(float elapsed, bool expected)
        {
            Assert.That(DodgeTiming.IsProtected(elapsed, 0.06f, 0.2f), Is.EqualTo(expected));
        }

        [Test]
        public void DodgeStaminaSpendingIsAllOrNothing()
        {
            var host = new GameObject("Dodge stamina test");
            var stats = ScriptableObject.CreateInstance<CharacterStatsDefinition>();
            try
            {
                host.AddComponent<CharacterStats>().SetDefinition(stats);
                var stamina = host.AddComponent<CharacterStamina>();
                for (int i = 0; i < 4; i++) Assert.IsTrue(stamina.TrySpend(25f));
                Assert.IsFalse(stamina.TrySpend(25f));
                Assert.That(stamina.Current, Is.Zero);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(stats); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ControllerFollowsJumpLandingAndDodgeGameplayState(bool heavy)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab").GetComponentInChildren<Animator>(true);
            var copy = Object.Instantiate(source.gameObject);
            var graph = PlayableGraph.Create("Airborne controller test");
            try
            {
                var animator = copy.GetComponent<Animator>();
                var controller = animator.runtimeAnimatorController;
                animator.runtimeAnimatorController = null;
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "Player", animator).SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
                playable.SetBool("IsGrounded", true);
                Advance(graph, 10);
                playable.SetBool("IsGrounded", false); playable.SetFloat("VerticalVelocity", 5f);
                Advance(graph, 12);
                Assert.IsTrue(playable.GetCurrentAnimatorStateInfo(0).IsName("Jump Takeoff"));
                playable.SetFloat("VerticalVelocity", -2f); Advance(graph, 12);
                Assert.IsTrue(playable.GetCurrentAnimatorStateInfo(0).IsName("Falling"));
                playable.SetBool("HardLanding", heavy); playable.SetBool("IsGrounded", true);
                Advance(graph, 8);
                Assert.IsTrue(playable.GetCurrentAnimatorStateInfo(0).IsName(heavy ? "Heavy Landing" : "Soft Landing"));
                Advance(graph, 120);
                Assert.IsTrue(playable.GetCurrentAnimatorStateInfo(0).IsName("Ground Locomotion"));
                playable.SetFloat("DodgePlayback", 1f); playable.SetFloat("DodgeX", -1f);
                playable.SetBool("IsDodging", true); Advance(graph, 10);
                Assert.IsTrue(playable.GetCurrentAnimatorStateInfo(0).IsName("Directional Dodge"));
                playable.SetBool("IsDodging", false); Advance(graph, 12);
                Assert.IsTrue(playable.GetCurrentAnimatorStateInfo(0).IsName("Ground Locomotion"));
            }
            finally { graph.Destroy(); Object.DestroyImmediate(copy); }
        }

        private static void Advance(PlayableGraph graph, int frames)
        {
            for (int i = 0; i < frames; i++) graph.Evaluate(1f / 60f);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void SprintDrainIsFrameRateIndependent(int fps)
        {
            var host = new GameObject("Stamina test");
            var stats = ScriptableObject.CreateInstance<CharacterStatsDefinition>();
            try
            {
                host.AddComponent<CharacterStats>().SetDefinition(stats);
                var stamina = host.AddComponent<CharacterStamina>();
                for (int frame = 0; frame < fps * 2; frame++) Assert.IsTrue(stamina.SpendSprint(18f / fps));
                Assert.That(stamina.Current, Is.EqualTo(64f).Within(0.01f));
                Assert.IsFalse(stamina.SpendSprint(100f));
                Assert.That(stamina.Current, Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(stats); }
        }

        [TestCase(0f)]
        [TestCase(2f / 7f)]
        [TestCase(4.5f / 7f)]
        [TestCase(1f)]
        public void SavedControllerAnimatesCurrentPlayerAtEachPace(float pace)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
            var source = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.runtimeAnimatorController.name, Is.EqualTo("KaelMovement"));
            Assert.IsTrue(source.avatar.isValid && source.avatar.isHuman);
            var copy = Object.Instantiate(source.gameObject);
            var graph = PlayableGraph.Create("Movement test");
            try
            {
                var animator = copy.GetComponent<Animator>();
                var controller = animator.runtimeAnimatorController;
                animator.runtimeAnimatorController = null;
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                var output = AnimationPlayableOutput.Create(graph, "Player", animator);
                output.SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                graph.Play();
                playable.SetFloat("MoveForward", pace);
                playable.SetBool("IsGrounded", true);
                graph.Evaluate(0.01f);
                var bone = animator.GetBoneTransform(pace == 0f ? HumanBodyBones.LeftHand : HumanBodyBones.LeftFoot);
                Quaternion before = bone.localRotation;
                float motion = 0f;
                for (int i = 0; i < 480; i++)
                {
                    graph.Evaluate(1f / 60f);
                    motion += Quaternion.Angle(before, bone.localRotation);
                }
                Assert.That(motion, Is.GreaterThan(0.01f), "Controller is not moving the Humanoid bones.");
            }
            finally { graph.Destroy(); Object.DestroyImmediate(copy); }
        }
    }
}
