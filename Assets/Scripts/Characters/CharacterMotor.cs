using System;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// Moves a <see cref="CharacterController"/>: gravity, grounding, jumping, and applying a
    /// requested horizontal velocity.
    /// </summary>
    /// <remarks>
    /// The motor has no opinion about where movement comes from. It never reads input and never
    /// looks at a camera - callers hand it a world-space velocity and it integrates. That is what
    /// lets the enemy in Milestone 5 use the same component driven by a navigation agent instead of
    /// a controller, rather than a second, subtly different movement implementation.
    ///
    /// <see cref="CharacterController"/> rather than <see cref="Rigidbody"/>: action-RPG movement
    /// wants authored, predictable motion, and a physics-driven character fights the designer on
    /// every slope and every knockback. The tradeoff is that real forces have to be faked; if
    /// levitation is ever cast on the player, that is the decision to revisit.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(MotorExecutionOrder)]
    public sealed class CharacterMotor : MonoBehaviour
    {
        /// <summary>
        /// Runs after default-order components so locomotion and abilities have already set the
        /// velocity for this frame before it is integrated.
        /// </summary>
        public const int MotorExecutionOrder = 100;

        [Header("Gravity")]
        [SerializeField]
        [Tooltip("Downward acceleration in m/s^2, as a positive magnitude. Earth is 9.81; games usually want more.")]
        private float gravity = 24f;

        [SerializeField]
        [Tooltip("Maximum fall speed. Caps the per-frame step so a long fall cannot tunnel through thin colliders.")]
        private float terminalFallSpeed = 45f;

        [SerializeField]
        [Tooltip("Downward velocity held while grounded to keep the controller pinned to slopes and steps.")]
        private float groundedStickSpeed = 2f;

        [Header("Grounding")]
        [SerializeField]
        [Tooltip("Extra distance below the controller checked for ground, on top of its own skin width.")]
        private float groundProbeDistance = 0.15f;

        [SerializeField] private LayerMask groundLayers = ~0;

        private CharacterController controller;
        private Vector3 horizontalVelocity;

        /// <summary>Raised on the frame the motor transitions from airborne to grounded, with the impact speed.</summary>
        public event Action<float> Landed;

        public event Action Jumped;

        public bool IsGrounded { get; private set; }

        /// <summary>Signed vertical speed. Negative is falling.</summary>
        public float VerticalVelocity { get; private set; }

        /// <summary>World-space velocity actually applied last tick.</summary>
        public Vector3 Velocity => horizontalVelocity + Vector3.up * VerticalVelocity;

        public float HorizontalSpeed => horizontalVelocity.magnitude;

        public CharacterController Controller => controller;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            IsGrounded = true;
        }

        /// <summary>Sets the horizontal velocity applied from the next <see cref="Tick"/> onward.</summary>
        public void SetHorizontalVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            horizontalVelocity = velocity;
        }

        /// <summary>Launches the character to reach approximately <paramref name="peakHeight"/> metres.</summary>
        public void Jump(float peakHeight)
        {
            VerticalVelocity = MotorMath.JumpSpeedForHeight(peakHeight, gravity);
            IsGrounded = false;
            Jumped?.Invoke();
        }

        /// <summary>Cuts an ascent short, for variable jump height on button release.</summary>
        public void CancelAscent(float retainedFraction = 0.35f)
        {
            if (VerticalVelocity > 0f)
            {
                VerticalVelocity *= Mathf.Clamp01(retainedFraction);
            }
        }

        /// <summary>
        /// Integration happens here and only here. Abilities set velocity and let the motor run;
        /// nothing else calls <see cref="CharacterController.Move"/>, so there is exactly one
        /// place where the character's position changes and no chance of double integration.
        /// </summary>
        private void Update() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            bool wasGrounded = IsGrounded;

            if (IsGrounded && VerticalVelocity <= 0f)
            {
                // A small constant downward push, not zero: zero lets the controller float off the
                // top of steps and ramps and report itself airborne every other frame.
                VerticalVelocity = -Mathf.Abs(groundedStickSpeed);
            }
            else
            {
                VerticalVelocity = MotorMath.ApplyGravity(VerticalVelocity, gravity, terminalFallSpeed, deltaTime);
            }

            Vector3 motion = (horizontalVelocity + Vector3.up * VerticalVelocity) * deltaTime;
            controller.Move(motion);

            float impactSpeed = VerticalVelocity;
            IsGrounded = ProbeGrounded();

            if (IsGrounded && !wasGrounded)
            {
                Landed?.Invoke(Mathf.Abs(impactSpeed));
            }
        }

        /// <summary>
        /// Ground test. <see cref="CharacterController.isGrounded"/> alone is unreliable - it
        /// reports false for a frame when stepping off small ledges - so it is combined with an
        /// explicit sphere cast under the capsule.
        /// </summary>
        private bool ProbeGrounded()
        {
            if (VerticalVelocity > 0f)
            {
                return false;
            }

            if (controller.isGrounded)
            {
                return true;
            }

            float radius = controller.radius;
            Vector3 origin = transform.position + controller.center + Vector3.up * (radius - controller.skinWidth);
            float distance = radius + groundProbeDistance;

            return Physics.SphereCast(origin, radius * 0.95f, Vector3.down, out _, distance,
                groundLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Teleports the character, which a plain transform assignment cannot do safely.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            // The controller caches its own position; it must be disabled across the move or it
            // will snap straight back on the next internal update.
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = wasEnabled;

            horizontalVelocity = Vector3.zero;
            VerticalVelocity = 0f;
        }
    }
}
