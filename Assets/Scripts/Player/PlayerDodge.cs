using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Core.Debugging;
using Frieren.Core.Input;
using UnityEngine;

namespace Frieren.Player
{
    /// <summary>
    /// A short directional dash that takes exclusive control of the body while it runs.
    /// </summary>
    /// <remarks>
    /// The first holder of <see cref="CharacterActionLock"/>, and the reason it exists. While the
    /// dodge holds the lock, locomotion stands down and the dodge drives the motor directly.
    ///
    /// Damage is ignored only during the gameplay-owned invulnerability window.
    ///
    /// Speed falls off linearly rather than along an AnimationCurve. A curve would serialise as
    /// keyframe data in the prefab, and an empty curve evaluates to zero - a dodge that silently
    /// does nothing. Two floats cannot fail that way.
    /// </remarks>
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(CharacterActionLock))]
    [DisallowMultipleComponent]
    public sealed class PlayerDodge : MonoBehaviour, IDamageModifier
    {
        [Header("Input")]
        [SerializeField] private InputReader inputReader;

        [Header("Dash")]
        [SerializeField]
        [Tooltip("Speed at the start of the dodge, in metres per second.")]
        private float startSpeed = 14f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the start speed remaining at the end, so the dodge settles rather than stopping dead.")]
        private float endSpeedFraction = 0.25f;

        [SerializeField] private float duration = 0.35f;

        [SerializeField] private float cooldown = 0.5f;

        [Header("Rules")]
        [SerializeField]
        [Tooltip("Whether a dodge can be started while airborne.")]
        private bool allowInAir;

        [SerializeField]
        [Tooltip("Seconds after the dodge begins during which the character should ignore damage.")]
        private float invulnerabilityDuration = 0.2f;

        [SerializeField] private float invulnerabilityStart = 0.06f;
        [SerializeField] private float staminaCost = 25f;
        private CharacterStamina stamina;
        private PlayerLocomotion locomotion;

        private CharacterMotor motor;
        private CharacterActionLock actionLock;
        private ICharacterAnimation characterAnimation;

        private Vector3 dodgeDirection;
        private float elapsed;
        private float lastDodgeEndTime = float.NegativeInfinity;

        public bool IsDodging { get; private set; }

        /// <summary>True during the dodge's invulnerability window. Read by combat from Milestone 5.</summary>
        public bool IsInvulnerable => isActiveAndEnabled && IsDodging &&
            DodgeTiming.IsProtected(elapsed, invulnerabilityStart, invulnerabilityDuration);
        public int ModifierOrder => -1000;
        public float ModifyIncomingDamage(in DamageInfo damage, float amount) => IsInvulnerable ? 0f : amount;

        public bool IsOnCooldown => Time.time - lastDodgeEndTime < cooldown;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            actionLock = GetComponent<CharacterActionLock>();
            characterAnimation = GetComponent<ICharacterAnimation>();
            stamina = GetComponent<CharacterStamina>();
            locomotion = GetComponent<PlayerLocomotion>();

            if (inputReader == null)
            {
                GameLog.Error(LogChannel.Player,
                    $"{name}: PlayerDodge has no InputReader assigned, so dodge will never fire.", this);
            }

            if (startSpeed <= 0f || duration <= 0f)
            {
                GameLog.Error(LogChannel.Player,
                    $"{name}: PlayerDodge is inert (start speed {startSpeed}, duration {duration}).", this);
            }
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.DodgePerformed += OnDodgePressed;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.DodgePerformed -= OnDodgePressed;
            }

            if (IsDodging)
            {
                EndDodge();
            }
        }

        private void Update()
        {
            if (!IsDodging)
            {
                return;
            }

            elapsed += Time.deltaTime;

            if (elapsed >= duration)
            {
                EndDodge();
                return;
            }

            float progress = duration <= 0f ? 1f : elapsed / duration;
            float speed = startSpeed * Mathf.Lerp(1f, endSpeedFraction, progress);
            motor.SetHorizontalVelocity(dodgeDirection * speed);
        }

        private void OnDodgePressed()
        {
            if (IsDodging || IsOnCooldown || startSpeed <= 0f || duration <= 0f || stamina == null || stamina.Current < staminaCost)
            {
                return;
            }

            if (!allowInAir && !motor.IsGrounded)
            {
                return;
            }

            if (!actionLock.TryAcquire(this))
            {
                return;
            }

            if (!stamina.TrySpend(staminaCost))
            {
                actionLock.Release(this);
                return;
            }

            dodgeDirection = ResolveDirection();
            elapsed = 0f;
            IsDodging = true;

            // Preserve target-facing in combat so left/right/back evasion remains directional.
            if (locomotion == null || locomotion.FaceTarget == null)
                transform.rotation = Quaternion.LookRotation(dodgeDirection, Vector3.up);
            motor.SetHorizontalVelocity(dodgeDirection * startSpeed);
            if (characterAnimation is MecanimCharacterAnimation mecanim)
                mecanim.SetDodge(true, dodgeDirection, duration);
            characterAnimation?.PlayAction(CharacterAction.Dodge);

            GameLog.Info(LogChannel.Player, $"Dodge started toward {dodgeDirection}.", this);
        }

        /// <summary>Dodges toward the stick if it is held, otherwise straight ahead.</summary>
        private Vector3 ResolveDirection()
        {
            Vector2 input = inputReader != null ? inputReader.MoveInput : Vector2.zero;

            if (input.sqrMagnitude > 0.01f)
            {
                UnityEngine.Camera main = UnityEngine.Camera.main;
                Quaternion reference = main != null ? main.transform.rotation : transform.rotation;
                Vector3 direction = MotorMath.CameraRelativeDirection(input, reference);
                if (locomotion != null && locomotion.FaceTarget != null)
                {
                    Vector3 toward = locomotion.FaceTarget.position - transform.position;
                    toward.y = 0f;
                    if (toward.sqrMagnitude > 0.0001f)
                        direction = toward.normalized * input.y + Vector3.Cross(Vector3.up, toward.normalized) * input.x;
                }

                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }

            Vector3 forward = locomotion != null && locomotion.FaceTarget != null ? -transform.forward : transform.forward;
            forward.y = 0f;

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private void EndDodge()
        {
            IsDodging = false;
            elapsed = 0f;
            lastDodgeEndTime = Time.time;
            if (characterAnimation is MecanimCharacterAnimation mecanim)
                mecanim.SetDodge(false, dodgeDirection, duration);

            // Hand the body back with the dodge's exit speed intact; locomotion picks it up from
            // the motor next frame rather than snapping to zero.
            actionLock.Release(this);
        }
    }
}
