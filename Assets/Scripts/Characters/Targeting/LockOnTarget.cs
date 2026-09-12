using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Characters.Targeting
{
    /// <summary>
    /// Marks something the player can lock the camera onto, and says where on it to aim.
    /// </summary>
    /// <remarks>
    /// A component rather than a layer test, because "lockable" and "enemy" are not the same set
    /// and conflating them costs a rewrite later. A boss's weak point, a floating rune, a training
    /// dummy and a bird worth watching are all things a camera should be able to hold on; none of
    /// them wants an <c>EnemyBrain</c>. It also keeps the direction of dependency honest:
    /// <c>Frieren.Player</c> does not reference <c>Frieren.Enemies</c> and must not start, so the
    /// thing the player locks onto has to live somewhere they both already reach.
    ///
    /// Targets keep a static register of themselves rather than being found with a physics query.
    /// A query has to guess a radius and a layer mask, allocates or needs a preallocated buffer,
    /// and quietly misses anything whose collider is not where the query expected; the register is
    /// exact, costs nothing per frame, and works for a target with no collider at all. The price is
    /// global mutable state, which is paid for by clearing it on subsystem registration - without
    /// that, entering play mode with domain reload disabled inherits the last session's corpses.
    ///
    /// <see cref="AimPosition"/> exists because <c>transform.position</c> is on the floor. Locking
    /// onto a character's feet points the camera at the ground in front of them.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LockOnTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Where the camera aims. Falls back to the transform raised by Aim Height.")]
        private Transform aimPoint;

        [SerializeField]
        [Tooltip("Height above the origin to aim at when no Aim Point is assigned. Roughly the chest.")]
        private float aimHeight = 1.1f;

        [SerializeField]
        [Tooltip("Shown by the lock-on reticle. Falls back to the object's name.")]
        private string displayName;

        private static readonly List<LockOnTarget> ActiveTargets = new List<LockOnTarget>();

        private CharacterHealth health;
        private bool searchedForHealth;

        /// <summary>Every enabled target in the scene, in registration order.</summary>
        public static IReadOnlyList<LockOnTarget> Active => ActiveTargets;

        public Vector3 AimPosition => aimPoint != null
            ? aimPoint.position
            : transform.position + Vector3.up * aimHeight;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        /// <summary>False once the thing is dead, so the lock drops rather than tracking a corpse.</summary>
        public bool IsLockable
        {
            get
            {
                if (!isActiveAndEnabled)
                {
                    return false;
                }

                if (!searchedForHealth)
                {
                    // Deferred rather than cached in Awake: a target may be a child object whose
                    // health lives on a parent that is itself spawned, and GetComponentInParent at
                    // Awake time would find nothing and cache the nothing.
                    health = GetComponentInParent<CharacterHealth>();
                    searchedForHealth = true;
                }

                return health == null || health.IsAlive;
            }
        }

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }
        }

        private void OnDisable() => ActiveTargets.Remove(this);

        /// <summary>
        /// Entering play mode with domain reload disabled keeps statics from the last session, and
        /// a register full of destroyed targets makes the first lock-on of the session pick one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegister() => ActiveTargets.Clear();

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(AimPosition, 0.18f);
        }
#endif
    }
}
