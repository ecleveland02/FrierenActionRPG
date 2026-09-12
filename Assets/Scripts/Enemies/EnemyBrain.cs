using System;
using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Core.Debugging;
using UnityEngine;

namespace Frieren.Enemies
{
    public enum EnemyState
    {
        Idle = 0,
        Chase = 1,
        Attack = 2,
        Stagger = 3,
        Dead = 4
    }

    /// <summary>
    /// Decides what one enemy is doing: standing, closing, swinging, reeling, or finished.
    /// </summary>
    /// <remarks>
    /// Its own small state machine rather than the application-level <c>GameStateMachine</c>, which
    /// exists for menus and pausing. Sharing them would put "the game is loading" and "this wolf is
    /// staggered" in one enum.
    ///
    /// Movement goes through <see cref="CharacterMotor"/>, the same component the player uses, so
    /// gravity, grounding and step handling are not implemented twice. Steering is direct rather
    /// than navigated: a NavMesh needs baking, baking needs real level geometry, and a gray-box
    /// arena does not have any. Swap in an agent that writes to the same motor when it does.
    ///
    /// Being hit interrupts whatever it was doing. Without that, an enemy mid-swing ignores damage
    /// entirely and combat has no give and take.
    /// </remarks>
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(CharacterHealth))]
    [RequireComponent(typeof(CharacterActionLock))]
    [RequireComponent(typeof(EnemyPerception))]
    [DisallowMultipleComponent]
    public sealed class EnemyBrain : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.2f;

        [SerializeField] private float turnSpeed = 480f;

        [SerializeField]
        [Tooltip("Stops this far short of its reach, so it is not shoving the player while swinging.")]
        private float standoff = 0.4f;

        [Header("Reactions")]
        [SerializeField]
        [Tooltip("Seconds spent reeling after being hit. This is the player's opening.")]
        private float staggerDuration = 0.45f;

        [SerializeField]
        [Tooltip("Smallest gap between staggers, so rapid hits cannot lock the enemy permanently.")]
        private float staggerCooldown = 0.9f;

        [Header("Death")]
        [SerializeField]
        [Tooltip("Seconds the body remains after dying before it is disabled.")]
        private float corpseDuration = 2.5f;

        [Header("Scanning")]
        [SerializeField]
        [Tooltip("Seconds between searches for a target while idle. Perception is not free.")]
        private float scanInterval = 0.25f;

        private CharacterMotor motor;
        private CharacterHealth health;
        private CharacterActionLock actionLock;
        private EnemyPerception perception;
        private EnemyMelee melee;
        private EnemyNavigation navigation;
        private ICharacterAnimation characterAnimation;

        private float stateEndsAt;
        private float nextStaggerAllowedAt;
        private float nextScanAt;

        public event Action<EnemyState, EnemyState> StateChanged;

        public EnemyState State { get; private set; } = EnemyState.Idle;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            health = GetComponent<CharacterHealth>();
            actionLock = GetComponent<CharacterActionLock>();
            perception = GetComponent<EnemyPerception>();
            melee = GetComponent<EnemyMelee>();
            navigation = GetComponent<EnemyNavigation>();
            characterAnimation = GetComponent<ICharacterAnimation>();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (State == EnemyState.Dead)
            {
                TickDead();
                return;
            }

            Scan();

            switch (State)
            {
                case EnemyState.Idle:
                    TickIdle();
                    break;
                case EnemyState.Chase:
                    TickChase();
                    break;
                case EnemyState.Attack:
                    TickAttack();
                    break;
                case EnemyState.Stagger:
                    TickStagger();
                    break;
            }
        }

        private void Scan()
        {
            if (Time.time < nextScanAt)
            {
                return;
            }

            nextScanAt = Time.time + Mathf.Max(0f, scanInterval);
            perception.Tick(perception.FindCandidate());
        }

        private void TickIdle()
        {
            motor.SetHorizontalVelocity(Vector3.zero);

            if (perception.HasTarget)
            {
                ChangeTo(EnemyState.Chase);
            }
        }

        private void TickChase()
        {
            if (!perception.HasTarget)
            {
                ChangeTo(EnemyState.Idle);
                return;
            }

            float reach = melee != null ? melee.Range : 2f;
            float distance = perception.PlanarDistanceToTarget();

            if (distance <= reach - standoff)
            {
                ChangeTo(EnemyState.Attack);
                return;
            }

            Vector3 toTarget = perception.Target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 direction = navigation != null
                ? navigation.DirectionTo(perception.Target.position) : toTarget.normalized;
            motor.SetHorizontalVelocity(direction * moveSpeed);
            if (direction.sqrMagnitude > 0.0001f) FaceTowards(direction);
        }

        private void TickAttack()
        {
            motor.SetHorizontalVelocity(Vector3.zero);

            if (!perception.HasTarget)
            {
                ChangeTo(EnemyState.Idle);
                return;
            }

            Vector3 toTarget = perception.Target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                FaceTowards(toTarget.normalized);
            }

            if (melee == null)
            {
                ChangeTo(EnemyState.Chase);
                return;
            }

            // Only leave once the swing has finished, or an enemy backs off mid-attack the instant
            // the player steps away, which looks like it changed its mind.
            if (melee.IsSwinging)
            {
                return;
            }

            if (perception.PlanarDistanceToTarget() > melee.Range)
            {
                ChangeTo(EnemyState.Chase);
                return;
            }

            melee.TrySwing();
        }

        private void TickStagger()
        {
            motor.SetHorizontalVelocity(Vector3.zero);

            if (Time.time < stateEndsAt)
            {
                return;
            }

            actionLock.Release(this);
            ChangeTo(perception.HasTarget ? EnemyState.Chase : EnemyState.Idle);
        }

        private void TickDead()
        {
            motor.SetHorizontalVelocity(Vector3.zero);

            if (corpseDuration > 0f && Time.time >= stateEndsAt)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDamaged(DamageInfo damage, float taken)
        {
            if (State == EnemyState.Dead || Time.time < nextStaggerAllowedAt)
            {
                return;
            }

            melee?.Abort();
            actionLock.ForceRelease();

            if (!actionLock.TryAcquire(this))
            {
                return;
            }

            nextStaggerAllowedAt = Time.time + staggerCooldown;
            stateEndsAt = Time.time + staggerDuration;
            characterAnimation?.PlayAction(CharacterAction.Hit);

            // Being hit reveals the attacker even if it was struck from behind.
            if (!perception.HasTarget && damage.Source != null)
            {
                perception.ForceTarget(damage.Source.transform);
            }

            ChangeTo(EnemyState.Stagger);
        }

        private void OnDied(DamageInfo damage)
        {
            melee?.Abort();
            actionLock.ForceRelease();
            motor.SetHorizontalVelocity(Vector3.zero);
            stateEndsAt = Time.time + corpseDuration;
            characterAnimation?.PlayAction(CharacterAction.Death);
            ChangeTo(EnemyState.Dead);
        }

        private void FaceTowards(Vector3 direction)
        {
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private void ChangeTo(EnemyState next)
        {
            if (State == next)
            {
                return;
            }

            EnemyState previous = State;
            State = next;
            GameLog.Info(LogChannel.AI, $"{name}: {previous} -> {next}.", this);
            StateChanged?.Invoke(previous, next);
        }
    }
}
