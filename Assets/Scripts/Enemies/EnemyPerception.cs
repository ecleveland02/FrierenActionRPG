using Frieren.Core;
using UnityEngine;

namespace Frieren.Enemies
{
    /// <summary>
    /// Decides whether an enemy can currently see its quarry.
    /// </summary>
    /// <remarks>
    /// Three tests, cheapest first: range, then field of view, then line of sight. Sight is the only
    /// one that costs a raycast, so it runs last and only when the other two already passed.
    ///
    /// Losing sight uses a longer range than gaining it, and a grace period before the enemy gives
    /// up. Without that hysteresis an enemy standing at exactly the detection distance flickers
    /// between states every frame, which looks broken and thrashes whatever listens to the change.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyPerception : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Where sight is measured from. Defaults to this transform raised by the eye height.")]
        private Transform eyes;

        [SerializeField] private float eyeHeight = 1.5f;

        [Header("Detection")]
        [SerializeField] private float sightRange = 14f;

        [SerializeField]
        [Range(1f, 360f)]
        [Tooltip("Total cone width in degrees.")]
        private float fieldOfView = 140f;

        [SerializeField]
        [Tooltip("Range at which an already-seen target is lost. Larger than sight range on purpose, " +
                 "so a target that shoots from beyond notice still gets chased.")]
        private float loseRange = 24f;

        [SerializeField]
        [Tooltip("Seconds of no sight before the target is forgotten, so a pillar does not reset the fight.")]
        private float loseDelay = 3f;

        [Header("Filtering")]
        [SerializeField] private LayerMask targetLayers = GameLayers.PlayerMask;

        [SerializeField]
        [Tooltip("What blocks sight. Should not include the target's own layer.")]
        private LayerMask sightBlockers = GameLayers.SolidMask;

        private float lastSeenTime = float.NegativeInfinity;

        public Transform Target { get; private set; }

        public bool HasTarget => Target != null;

        /// <summary>True only while the target is actually visible this frame.</summary>
        public bool CanSeeTarget { get; private set; }

        public Vector3 EyePosition => eyes != null ? eyes.position : transform.position + Vector3.up * eyeHeight;

        /// <summary>Points the enemy at a specific target, bypassing detection. For scripted fights.</summary>
        public void ForceTarget(Transform target)
        {
            Target = target;
            lastSeenTime = Time.time;
        }

        public void Forget()
        {
            Target = null;
            CanSeeTarget = false;
        }

        /// <summary>Distance to the target on the ground plane, ignoring height.</summary>
        public float PlanarDistanceToTarget()
        {
            if (Target == null)
            {
                return float.PositiveInfinity;
            }

            Vector3 offset = Target.position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        public void Tick(Transform candidate)
        {
            // No candidate is not the same as no target. Something that shot from out of the search
            // radius is still worth walking towards, so an existing target keeps being evaluated.
            Transform subject = candidate != null ? candidate : Target;

            if (subject == null)
            {
                Forget();
                return;
            }

            CanSeeTarget = CanSee(subject);

            if (CanSeeTarget)
            {
                Target = subject;
                lastSeenTime = Time.time;
                return;
            }

            if (Target == null)
            {
                return;
            }

            bool tooFar = Vector3.Distance(transform.position, Target.position) > loseRange;

            if (tooFar || Time.time - lastSeenTime > loseDelay)
            {
                Forget();
            }
        }

        /// <summary>Finds the nearest thing on the target layers, so the brain does not need a reference.</summary>
        public Transform FindCandidate()
        {
            Collider[] found = Physics.OverlapSphere(transform.position, loseRange, targetLayers,
                QueryTriggerInteraction.Ignore);

            Transform nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null || found[i].transform.IsChildOf(transform))
                {
                    continue;
                }

                float distance = Vector3.SqrMagnitude(found[i].transform.position - transform.position);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = found[i].transform;
                }
            }

            return nearest;
        }

        private bool CanSee(Transform candidate)
        {
            Vector3 origin = EyePosition;
            Vector3 toTarget = candidate.position + Vector3.up * 1f - origin;
            float distance = toTarget.magnitude;

            // Already engaged targets are tracked out to the larger radius; new ones must be closer.
            float allowed = Target == candidate ? loseRange : sightRange;

            if (distance > allowed)
            {
                return false;
            }

            Vector3 flat = toTarget;
            flat.y = 0f;

            if (flat.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flat.normalized) > fieldOfView * 0.5f)
            {
                return false;
            }

            return !Physics.Raycast(origin, toTarget.normalized, distance - 0.25f,
                sightBlockers, QueryTriggerInteraction.Ignore);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, loseRange);

            Vector3 left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawRay(EyePosition, left * sightRange);
            Gizmos.DrawRay(EyePosition, right * sightRange);
        }
#endif
    }
}
