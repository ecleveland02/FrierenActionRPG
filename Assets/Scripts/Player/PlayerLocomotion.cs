using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Core.Debugging;
using Frieren.Core.Input;
using UnityEngine;

namespace Frieren.Player
{
    /// <summary>
    /// Turns player input into a movement intent and hands it to the <see cref="CharacterMotor"/>.
    /// </summary>
    /// <remarks>
    /// This class decides <em>where the player wants to go</em>. It does not move anything: the
    /// motor integrates, and it is the only thing that does. Keeping that split means an enemy, a
    /// cutscene or a knockback can drive the same body without this component being involved.
    ///
    /// It also never acquires the action lock - it simply stands down while anyone else holds it.
    /// Movement is the default state of a character, not an action competing for the body.
    ///
    /// No explicit check against the game state is needed for pausing: entering Paused disables the
    /// gameplay action map and zeroes the cached input, and a zero time scale stops the motor
    /// anyway.
    /// </remarks>
    [RequireComponent(typeof(CharacterMotor))]
    [DisallowMultipleComponent]
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputReader inputReader;

        [Header("Speeds")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 8f;

        [SerializeField]
        [Tooltip("Metres per second squared while speeding up.")]
        private float acceleration = 45f;

        [SerializeField]
        [Tooltip("Metres per second squared while slowing down. Higher than acceleration feels responsive.")]
        private float deceleration = 60f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of ground acceleration available in the air.")]
        private float airControl = 0.35f;

        [Header("Turning")]
        [SerializeField]
        [Tooltip("Degrees per second the character turns toward its movement direction.")]
        private float turnSpeed = 720f;

        [Header("Jump")]
        [SerializeField]
        [Tooltip("Approximate peak height in metres.")]
        private float jumpHeight = 1.6f;

        [SerializeField]
        [Tooltip("Grace period after walking off a ledge during which a jump still counts.")]
        private float coyoteTime = 0.12f;

        [SerializeField]
        [Tooltip("How long a jump press stays queued while still airborne.")]
        private float jumpBufferTime = 0.15f;

        private CharacterMotor motor;
        private CharacterActionLock actionLock;
        private ICharacterAnimation characterAnimation;
        private JumpGate jumpGate;
        private Transform cameraTransform;
        private Vector3 planarVelocity;
        private bool hasReportedFirstMovement;

        /// <summary>Speed as a fraction of sprint speed. Drives the animation blend.</summary>
        public float NormalizedSpeed => sprintSpeed <= 0f ? 0f : Mathf.Clamp01(planarVelocity.magnitude / sprintSpeed);

        public bool IsSprinting { get; private set; }

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            actionLock = GetComponent<CharacterActionLock>();
            characterAnimation = GetComponent<ICharacterAnimation>();
            jumpGate = new JumpGate(coyoteTime, jumpBufferTime);
            WarnAboutConfigurationThatCannotWork();
        }

        /// <summary>
        /// Says out loud when this component is configured such that it cannot possibly move
        /// anything.
        /// </summary>
        /// <remarks>
        /// Both of these previously failed in complete silence: no input reader meant every frame
        /// read a zero input, and a zero walk speed meant every frame asked for zero velocity. The
        /// symptom in each case is a character that stands still with an empty console, which is
        /// the most expensive kind of bug to diagnose from the outside.
        /// </remarks>
        private void WarnAboutConfigurationThatCannotWork()
        {
            if (inputReader == null)
            {
                GameLog.Error(LogChannel.Player,
                    $"{name}: PlayerLocomotion has no InputReader assigned, so it will never move.", this);
            }

            if (walkSpeed <= 0f || sprintSpeed <= 0f)
            {
                GameLog.Error(LogChannel.Player,
                    $"{name}: PlayerLocomotion speeds are zero (walk {walkSpeed}, sprint {sprintSpeed}). " +
                    "Serialized values did not survive loading; check the prefab in the Inspector.", this);
            }
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.JumpPerformed += OnJumpPressed;
            }

            if (motor != null)
            {
                motor.Landed += OnLanded;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.JumpPerformed -= OnJumpPressed;
            }

            if (motor != null)
            {
                motor.Landed -= OnLanded;
            }

            jumpGate?.Reset();
        }

        /// <summary>
        /// Sets the camera that movement is interpreted relative to. Called by the spawner; falls
        /// back to the main camera so the prefab still works when dropped straight into a scene.
        /// </summary>
        public void SetCameraReference(Transform cameraToFollow)
        {
            cameraTransform = cameraToFollow;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            if (motor.IsGrounded)
            {
                jumpGate.NotifyGrounded(Time.time);
            }

            if (IsUnderExternalControl())
            {
                // Another ability owns the body. Keep our cached velocity in step with reality so
                // there is no snap when control returns.
                planarVelocity = motor.Velocity;
                planarVelocity.y = 0f;
                UpdateAnimation();
                return;
            }

            ApplyMovement(deltaTime);
            ApplyRotation(deltaTime);
            TryJump();
            UpdateAnimation();
        }

        private bool IsUnderExternalControl() => actionLock != null && actionLock.IsLocked;

        private void ApplyMovement(float deltaTime)
        {
            Vector2 input = inputReader != null ? inputReader.MoveInput : Vector2.zero;
            Quaternion cameraRotation = ResolveCameraRotation();
            Vector3 direction = MotorMath.CameraRelativeDirection(input, cameraRotation);

            IsSprinting = inputReader != null && inputReader.SprintHeld && direction.sqrMagnitude > 0.01f;

            float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = direction * targetSpeed;

            bool slowingDown = targetVelocity.sqrMagnitude < planarVelocity.sqrMagnitude;
            float rate = slowingDown ? deceleration : acceleration;

            if (!motor.IsGrounded)
            {
                rate *= airControl;
            }

            planarVelocity = MotorMath.MoveTowardsRate(planarVelocity, targetVelocity, rate, deltaTime);
            motor.SetHorizontalVelocity(planarVelocity);

            ReportFirstMovementAttempt(input, direction, targetSpeed);
        }

        /// <summary>
        /// Logs the whole movement path once, the first time a real input arrives.
        /// </summary>
        /// <remarks>
        /// Temporary. A character that does not move looks identical whether this component never
        /// ran, ran with a zero direction, computed a velocity the motor ignored, or moved somewhere
        /// off camera - and each needs a different fix. One line naming every value in the chain
        /// collapses that to a single observation. Delete once movement is confirmed working.
        /// </remarks>
        private void ReportFirstMovementAttempt(Vector2 input, Vector3 direction, float targetSpeed)
        {
            if (hasReportedFirstMovement || input.sqrMagnitude <= 0.01f)
            {
                return;
            }

            hasReportedFirstMovement = true;

            GameLog.Info(LogChannel.Player,
                $"First movement: input {input} -> direction {direction} at {targetSpeed} m/s, " +
                $"velocity {planarVelocity}, grounded {motor.IsGrounded}, " +
                $"locked {(actionLock != null && actionLock.IsLocked)}, " +
                $"camera '{(cameraTransform != null ? cameraTransform.name : "none")}', " +
                $"position {transform.position}, controller enabled {motor.Controller.enabled}.", this);
        }

        private void ApplyRotation(float deltaTime)
        {
            if (planarVelocity.sqrMagnitude <= 0.01f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * deltaTime);
        }

        private void TryJump()
        {
            if (!jumpGate.TryConsume(Time.time))
            {
                return;
            }

            motor.Jump(jumpHeight);
            characterAnimation?.PlayAction(CharacterAction.Jump);
        }

        private Quaternion ResolveCameraRotation()
        {
            if (cameraTransform != null)
            {
                return cameraTransform.rotation;
            }

            UnityEngine.Camera main = UnityEngine.Camera.main;

            if (main != null)
            {
                cameraTransform = main.transform;
                return cameraTransform.rotation;
            }

            // No camera at all: treat input as world-relative rather than refusing to move.
            return Quaternion.identity;
        }

        private void UpdateAnimation()
        {
            characterAnimation?.SetLocomotion(
                planarVelocity.magnitude,
                NormalizedSpeed,
                motor.IsGrounded,
                motor.VerticalVelocity);
        }

        private void OnJumpPressed() => jumpGate.NotifyJumpPressed(Time.time);

        private void OnLanded(float impactSpeed) => characterAnimation?.PlayAction(CharacterAction.Land);
    }
}
