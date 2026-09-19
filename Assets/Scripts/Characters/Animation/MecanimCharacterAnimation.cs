using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Characters.Animation
{
    /// <summary>
    /// Drives a Unity <see cref="Animator"/>. The implementation the rigged character will use.
    /// </summary>
    /// <remarks>
    /// Parameter names are serialized rather than hard-coded so the controller authored in
    /// Milestone 3 does not have to match names chosen here. Every parameter is resolved to a hash
    /// once and checked for existence, so a controller missing "Dodge" logs once and carries on
    /// instead of throwing every frame.
    ///
    /// An action with no matching trigger is silently ignored, which is the right default for a
    /// character that genuinely cannot do something. Kael has no Attack or Death clip: he is a
    /// caster, and his death is handled by <c>DeathSink</c> tipping the body rather than by an
    /// animation. Adding either later means adding a trigger to the controller and nothing here.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MecanimCharacterAnimation : MonoBehaviour, ICharacterAnimation
    {
        [SerializeField] private Animator animator;

        [Header("Float parameters")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string normalizedSpeedParameter = "MoveBlend";
        [SerializeField] private string directionParameter = "MoveDirection";
        [SerializeField] private string forwardParameter = "MoveForward";
        [SerializeField] private string verticalVelocityParameter = "VerticalVelocity";

        [Header("Bool parameters")]
        [SerializeField] private string groundedParameter = "IsGrounded";
        [SerializeField] private string sprintParameter = "IsSprinting";

        [Header("Damping")]
        [SerializeField]
        [Tooltip("Seconds of smoothing applied to the blend parameter, so the blend tree does not pop.")]
        private float blendDampTime = 0.1f;

        private int speedHash;
        private int normalizedSpeedHash;
        private int directionHash;
        private int forwardHash;
        private int verticalVelocityHash;
        private int groundedHash;
        private int sprintHash;
        private bool warnedMissingAnimator;
        private readonly int turnHash = Animator.StringToHash("Turn");
        public void SetLanding(float impactSpeed)
        {
            if (IsUsable()) SetBool(Animator.StringToHash("HardLanding"), impactSpeed >= 11f);
        }

        public void SetDodge(bool active, Vector3 worldDirection, float duration)
        {
            if (!IsUsable()) return;
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            SetFloat(Animator.StringToHash("DodgeX"), local.x);
            SetFloat(Animator.StringToHash("DodgeY"), local.z);
            SetFloat(Animator.StringToHash("DodgePlayback"), 0.45f / Mathf.Max(0.01f, duration));
            SetBool(Animator.StringToHash("IsDodging"), active);
        }
        public void SetTurnRate(float degreesPerSecond)
        {
            if (IsUsable()) SetFloatDamped(turnHash, Mathf.Clamp(degreesPerSecond / 180f, -1f, 1f));
        }

        // Animator.parameters allocates a fresh array on every access, and this component touched
        // it four times a frame per character. Cached once against the controller it was built
        // from, so a controller swapped at runtime still rebuilds it.
        private readonly Dictionary<int, AnimatorControllerParameterType> parameterTypes =
            new Dictionary<int, AnimatorControllerParameterType>();

        private RuntimeAnimatorController cachedFor;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            speedHash = Animator.StringToHash(speedParameter);
            normalizedSpeedHash = Animator.StringToHash(normalizedSpeedParameter);
            directionHash = Animator.StringToHash(directionParameter);
            forwardHash = Animator.StringToHash(forwardParameter);
            verticalVelocityHash = Animator.StringToHash(verticalVelocityParameter);
            groundedHash = Animator.StringToHash(groundedParameter);
            sprintHash = Animator.StringToHash(sprintParameter);
        }

        public void SetLocomotion(float planarSpeed, float normalizedSpeed, bool isGrounded, float verticalVelocity)
        {
            if (!IsUsable())
            {
                return;
            }

            SetFloat(speedHash, planarSpeed);
            SetFloatDamped(normalizedSpeedHash, normalizedSpeed);
            SetFloat(verticalVelocityHash, verticalVelocity);
            SetBool(groundedHash, isGrounded);
            SetBool(sprintHash, normalizedSpeed > 0.8f);
        }

        public void SetMovementDirection(Vector3 worldVelocity, bool strafe = true, float referenceSpeed = 8f)
        {
            if (!IsUsable()) return;
            Vector3 local = strafe ? transform.InverseTransformDirection(worldVelocity) : Vector3.forward * worldVelocity.magnitude;
            float scale = Mathf.Max(0.01f, referenceSpeed);
            SetFloatDamped(directionHash, Mathf.Clamp(local.x / scale, -1f, 1f));
            SetFloatDamped(forwardHash, Mathf.Clamp(local.z / scale, -1f, 1f));
        }

        public void PlayAction(CharacterAction action)
        {
            if (!IsUsable())
            {
                return;
            }

            int hash = Animator.StringToHash(action.ToString());

            if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(hash);
            }
        }

        private bool IsUsable()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                return true;
            }

            if (!warnedMissingAnimator)
            {
                warnedMissingAnimator = true;
                Debug.LogWarning(
                    $"{name}: MecanimCharacterAnimation has no Animator with a controller. " +
                    "Use PlaceholderCharacterAnimation until the rigged character exists.", this);
            }

            return false;
        }

        private void SetFloat(int hash, float value)
        {
            if (HasParameter(hash, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(hash, value);
            }
        }

        private void SetFloatDamped(int hash, float value)
        {
            if (HasParameter(hash, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(hash, value, blendDampTime, Time.deltaTime);
            }
        }

        private void SetBool(int hash, bool value)
        {
            if (HasParameter(hash, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(hash, value);
            }
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            if (cachedFor != animator.runtimeAnimatorController)
            {
                CacheParameters();
            }

            return parameterTypes.TryGetValue(hash, out AnimatorControllerParameterType found)
                   && found == type;
        }

        private void CacheParameters()
        {
            parameterTypes.Clear();
            cachedFor = animator.runtimeAnimatorController;

            AnimatorControllerParameter[] parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[parameters[i].nameHash] = parameters[i].type;
            }
        }
    }
}
