using System;
using Frieren.Characters;
using Frieren.Core;
using Frieren.Core.Debugging;
using UnityEngine;

namespace Frieren.Enemies
{
    /// <summary>
    /// A telegraphed melee swing: wind up, strike, recover.
    /// </summary>
    /// <remarks>
    /// The wind-up is the point. An attack that lands the instant it starts cannot be dodged, which
    /// makes the dodge built in Milestone 2 pointless and the fight a damage race. The delay is what
    /// turns it into a decision.
    ///
    /// The hit is tested at the moment of the strike, not when the swing began, so stepping out of
    /// range during the wind-up avoids it. That is the same reason the dodge exists.
    /// </remarks>
    [RequireComponent(typeof(CharacterActionLock))]
    [DisallowMultipleComponent]
    public sealed class EnemyMelee : MonoBehaviour
    {
        private const int MaxHits = 8;

        [Header("Reach")]
        [SerializeField] private float range = 2.2f;

        [SerializeField]
        [Tooltip("Radius of the hit test at the moment of the strike.")]
        private float hitRadius = 1.1f;

        [SerializeField]
        [Tooltip("How far in front of the enemy the hit is centred.")]
        private float hitForwardOffset = 1.2f;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Seconds between starting the swing and it landing. This is the dodge window.")]
        private float windUp = 0.55f;

        [SerializeField]
        [Tooltip("Seconds after the strike before the enemy can act again.")]
        private float recovery = 0.6f;

        [SerializeField] private float cooldown = 1.4f;

        [Header("Damage")]
        [SerializeField] private float damage = 12f;

        [SerializeField] private DamageType damageType = DamageType.Physical;

        [SerializeField] private LayerMask hits = GameLayers.PlayerMask;

        private readonly Collider[] overlap = new Collider[MaxHits];
        private CharacterActionLock actionLock;
        private float readyAt;
        private float phaseEndsAt;

        public event Action SwingStarted;

        public event Action<int> SwingLanded;

        public bool IsSwinging { get; private set; }

        /// <summary>True during the wind-up, when a player still has time to move.</summary>
        public bool IsWindingUp { get; private set; }

        public float Range => range;

        public bool IsReady => Time.time >= readyAt && !IsSwinging;

        private void Awake() => actionLock = GetComponent<CharacterActionLock>();

        private void OnDisable() => Abort();

        /// <summary>Starts a swing if one is not already running and the cooldown has elapsed.</summary>
        public bool TrySwing()
        {
            if (!IsReady || !actionLock.TryAcquire(this))
            {
                return false;
            }

            IsSwinging = true;
            IsWindingUp = true;
            phaseEndsAt = Time.time + windUp;
            SwingStarted?.Invoke();
            return true;
        }

        /// <summary>Cancels a swing in progress, as a stagger should.</summary>
        public void Abort()
        {
            if (!IsSwinging)
            {
                return;
            }

            IsSwinging = false;
            IsWindingUp = false;
            readyAt = Time.time + cooldown;
            actionLock.Release(this);
        }

        private void Update()
        {
            if (!IsSwinging || Time.time < phaseEndsAt)
            {
                return;
            }

            if (IsWindingUp)
            {
                IsWindingUp = false;
                phaseEndsAt = Time.time + recovery;
                SwingLanded?.Invoke(Strike());
                return;
            }

            IsSwinging = false;
            readyAt = Time.time + cooldown;
            actionLock.Release(this);
        }

        private int Strike()
        {
            Vector3 centre = transform.position + Vector3.up * 1f + transform.forward * hitForwardOffset;
            int found = Physics.OverlapSphereNonAlloc(centre, hitRadius, overlap, hits,
                QueryTriggerInteraction.Ignore);
            var info = new DamageInfo(damage, damageType, gameObject);
            int landed = 0;

            for (int i = 0; i < found; i++)
            {
                if (overlap[i] == null || overlap[i].transform.IsChildOf(transform))
                {
                    continue;
                }

                var health = overlap[i].GetComponentInParent<CharacterHealth>();

                if (health == null || health.gameObject == gameObject || !health.IsAlive)
                {
                    continue;
                }

                health.TakeDamage(info);
                landed++;
            }

            GameLog.Info(LogChannel.Combat,
                landed > 0 ? $"{name} hit {landed} target(s)." : $"{name} swung and missed.", this);
            return landed;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * hitForwardOffset, hitRadius);
            Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
#endif
    }
}
