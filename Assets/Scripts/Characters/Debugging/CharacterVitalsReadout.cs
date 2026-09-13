using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Characters
{
    /// <summary>
    /// Draws a character's health and mana on screen, and provides keys to change them.
    /// </summary>
    /// <remarks>
    /// There is no HUD yet and nothing in the world that can hurt you, so without this the vitals
    /// are invisible and untestable in play. IMGUI for the same reason the core debug overlay uses
    /// it: no canvas, no prefab, no setup, and nothing to collide with the real UI later.
    ///
    /// This is the only reason <c>Frieren.Characters</c> references the Input System package. That
    /// is a package reference rather than a project one, so it does not affect the assembly
    /// dependency direction, but it should go when this component does - once a real HUD exists and
    /// enemies can deal damage.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterVitalsReadout : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float TestAmount = 15f;

        [SerializeField] private bool showKeys = true;

        private CharacterHealth health;
        private CharacterMana mana;
        private CharacterLevitation levitation;
        private CharacterMotor motor;
        private CharacterBarrier barrier;
        private Animator animator;
        private CharacterVisualAlign align;
        private int lastStateHash;
        private float lastStateTime;
        private bool clipAdvancing;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            mana = GetComponent<CharacterMana>();
            levitation = GetComponent<CharacterLevitation>();
            motor = GetComponent<CharacterMotor>();
            barrier = GetComponent<CharacterBarrier>();
            animator = GetComponentInChildren<Animator>();
            align = GetComponent<CharacterVisualAlign>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f2Key.wasPressedThisFrame && health != null)
            {
                health.TakeDamage(new DamageInfo(TestAmount, DamageType.Physical, gameObject));
            }

            if (keyboard.f3Key.wasPressedThisFrame && health != null)
            {
                if (health.IsAlive)
                {
                    health.Heal(TestAmount);
                }
                else
                {
                    health.Revive();
                }
            }

            if (keyboard.f4Key.wasPressedThisFrame && mana != null)
            {
                mana.TrySpend(TestAmount);
            }
        }

        /// <summary>
        /// Watches whether the animator's normalised time is actually moving.
        /// </summary>
        /// <remarks>
        /// A state name alone proves nothing. An animator sitting in Locomotion with a clip that
        /// failed to retarget looks identical, from outside, to one playing a perfectly good idle.
        /// Whether normalised time advances is the difference, and it is the single fact that
        /// separates "the controller is wrong" from "the clip never bound".
        /// </remarks>
        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            if (state.fullPathHash != lastStateHash)
            {
                lastStateHash = state.fullPathHash;
                lastStateTime = state.normalizedTime;
                return;
            }

            if (!Mathf.Approximately(state.normalizedTime, lastStateTime))
            {
                clipAdvancing = true;
            }

            lastStateTime = state.normalizedTime;
        }

        /// <summary>
        /// The rig, in one line, on screen. The console is where this belongs and is also where it
        /// goes unread; a line in the overlay is in every screenshot without anyone looking for it.
        /// </summary>
        private string DescribeRig()
        {
            if (animator == null)
            {
                return "Rig: no Animator on this character";
            }

            if (animator.runtimeAnimatorController == null)
            {
                return "Rig: Animator present, NO CONTROLLER";
            }

            if (animator.avatar == null)
            {
                return "Rig: NO AVATAR - nothing can play";
            }

            if (!animator.avatar.isValid)
            {
                return $"Rig: avatar {animator.avatar.name} is INVALID";
            }

            string kind = animator.avatar.isHuman ? "humanoid" : "GENERIC";
            string moving = clipAdvancing ? "advancing" : "FROZEN";

            return $"Rig: {kind} avatar, clip {moving}, " +
                   $"{animator.GetCurrentAnimatorClipInfoCount(0)} clip(s) bound to the current state";
        }

        private void OnGUI()
        {
            float height = (showKeys ? 76f : 56f)
                           + (levitation != null ? 18f : 0f)
                           + (barrier != null ? 18f : 0f)
                           + 36f;
            var area = new Rect(10f, 275f, 420f, height);
            GUILayout.BeginArea(area, GUI.skin.box);

            if (health != null)
            {
                GUILayout.Label($"Health {health.Current:0}/{health.Max:0}" +
                                $"   ({health.Normalized:P0})   {(health.IsAlive ? "alive" : "DEAD")}");
            }

            if (mana != null)
            {
                GUILayout.Label($"Mana   {mana.Current:0}/{mana.Max:0}   ({mana.Normalized:P0})");
            }

            if (levitation != null)
            {
                GUILayout.Label($"Levitating {(levitation.IsLevitating ? "YES" : "no")}" +
                                $"   vertical {(motor != null ? motor.VerticalVelocity : 0f):0.0} m/s" +
                                $"   gravity {(motor != null && motor.GravityEnabled ? "on" : "OFF")}");
            }

            if (barrier != null)
            {
                string state = barrier.IsUp
                    ? $"{barrier.Remaining:0} ({barrier.Normalized:P0})"
                    : "down";
                GUILayout.Label($"Barrier {state}");
            }

            if (showKeys)
            {
                GUILayout.Label($"F2 damage {TestAmount}   F3 heal / revive   F4 spend {TestAmount} mana");
            }

            GUILayout.Label(DescribeRig());

            if (align != null)
            {
                GUILayout.Label($"Visual lifted {align.AppliedOffset:0.000} m onto the collider base");
            }

            GUILayout.EndArea();
        }
#endif
    }
}
