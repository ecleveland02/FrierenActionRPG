using Frieren.Core.Input;
using UnityEngine;

namespace Frieren.Player.Cameras
{
    /// <summary>
    /// Third-person orbit camera: follows a target, orbits on look input, and pulls in when
    /// geometry would otherwise clip through it.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than Cinemachine. Cinemachine is the better long-term answer for complex
    /// framing, and this rig is not a substitute for it - it is deliberately about a hundred lines
    /// doing one job. Swapping to Cinemachine later means deleting this component and pointing the
    /// spawner at a Cinemachine target, which is a contained change.
    ///
    /// Runs in <c>LateUpdate</c> so the character has already moved this frame; following in Update
    /// produces a camera that is permanently one frame behind and visibly judders.
    /// </remarks>
    [RequireComponent(typeof(UnityEngine.Camera))]
    [DisallowMultipleComponent]
    public sealed class OrbitCameraRig : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputReader inputReader;

        [SerializeField]
        [Tooltip("Degrees per unit of mouse delta. Mouse input is already per-frame, so it is not scaled by time.")]
        private float pointerSensitivity = 12f;

        [SerializeField]
        [Tooltip("Degrees per second at full stick deflection. Scaled by delta time.")]
        private float stickSensitivity = 220f;

        [SerializeField] private bool invertY;

        [Header("Framing")]
        [SerializeField] private Transform target;

        [SerializeField]
        [Tooltip("Height above the target's origin the camera looks at, roughly chest height.")]
        private float pivotHeight = 1.4f;

        [SerializeField] private float distance = 5f;

        [SerializeField] private float minPitch = -30f;

        [SerializeField] private float maxPitch = 65f;

        [Header("Smoothing")]
        [SerializeField]
        [Tooltip("Seconds for the pivot to catch up with the target. Zero is rigid.")]
        private float followSmoothTime = 0.06f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionLayers = ~0;

        [SerializeField]
        [Tooltip("Radius of the sweep used to keep the camera out of walls.")]
        private float collisionRadius = 0.25f;

        [SerializeField]
        [Tooltip("Gap kept between the camera and whatever it hit.")]
        private float collisionBuffer = 0.15f;

        private const int MaxCollisionHits = 8;

        private readonly OrbitCameraSolver solver = new OrbitCameraSolver();
        private readonly RaycastHit[] castBuffer = new RaycastHit[MaxCollisionHits];

        private Vector3 smoothedPivot;
        private Vector3 pivotVelocity;
        private Vector2 pendingPointerDelta;
        private bool hasPivot;

        public Transform Target => target;

        public OrbitCameraSolver Solver => solver;

        private void Awake()
        {
            solver.SetPitchLimits(minPitch, maxPitch);
            solver.SetOrientation(transform.eulerAngles.y, transform.eulerAngles.x);
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.LookChanged += OnLookChanged;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.LookChanged -= OnLookChanged;
            }
        }

        /// <summary>Points the camera at a new target, snapping rather than sweeping to it.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            hasPivot = false;
            pendingPointerDelta = Vector2.zero;
        }

        /// <summary>
        /// Records the frame's pointer movement.
        /// </summary>
        /// <remarks>
        /// Mouse and stick have to be handled differently, and not only because of the units. A
        /// Value action fires only when its value <em>changes</em>, so a stick held at a constant
        /// deflection stops raising events entirely - driving the camera from this callback alone
        /// would make it stall mid-turn. The stick is therefore polled once per frame in
        /// <see cref="LateUpdate"/>.
        ///
        /// The mouse is assigned, not accumulated. A delta control sums its events <em>within</em>
        /// a frame and resets at the start of the next, so when several mouse events land in one
        /// frame each callback reports the running total rather than its own increment. Adding
        /// them up counts the same movement repeatedly: three events in a frame make the camera
        /// turn roughly twice as far as the mouse actually moved. The last callback of the frame
        /// already holds the whole delta.
        /// </remarks>
        private void OnLookChanged(Vector2 look)
        {
            if (inputReader != null && inputReader.LookIsPointerDelta)
            {
                pendingPointerDelta = look;
            }
        }

        private void ApplyLookThisFrame(float deltaTime)
        {
            Vector2 degrees = pendingPointerDelta * pointerSensitivity;
            pendingPointerDelta = Vector2.zero;

            if (inputReader != null && !inputReader.LookIsPointerDelta)
            {
                degrees += inputReader.LookInput * (stickSensitivity * deltaTime);
            }

            if (degrees.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            if (invertY)
            {
                degrees.y = -degrees.y;
            }

            solver.ApplyLook(degrees);
        }

        private void LateUpdate()
        {
            ApplyLookThisFrame(Time.deltaTime);

            if (target == null)
            {
                return;
            }

            Vector3 pivot = target.position + Vector3.up * pivotHeight;

            if (!hasPivot)
            {
                smoothedPivot = pivot;
                pivotVelocity = Vector3.zero;
                hasPivot = true;
            }
            else if (followSmoothTime > 0f)
            {
                smoothedPivot = Vector3.SmoothDamp(smoothedPivot, pivot, ref pivotVelocity, followSmoothTime);
            }
            else
            {
                smoothedPivot = pivot;
            }

            float allowedDistance = ResolveDistance(smoothedPivot);

            transform.SetPositionAndRotation(
                solver.DesiredPosition(smoothedPivot, allowedDistance),
                solver.Rotation);
        }

        /// <summary>
        /// Sweeps from the pivot toward where the camera wants to be and stops short of anything in
        /// the way. A sphere rather than a ray, so the camera does not clip a corner it passed beside.
        /// </summary>
        /// <remarks>
        /// The sweep starts inside the character's own capsule, so the single-hit overload is no
        /// use: it would report the player at zero distance and jam the camera on their head. Every
        /// hit is examined instead, discarding the target's own colliders and anything already
        /// overlapping at the start. Filtering by object rather than by layer means this keeps
        /// working whatever layer the character ends up on.
        /// </remarks>
        private float ResolveDistance(Vector3 pivot)
        {
            Vector3 direction = -(solver.Rotation * Vector3.forward);

            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, castBuffer,
                distance, collisionLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castBuffer[i];

                if (hit.distance <= 0f)
                {
                    continue;
                }

                if (target != null && hit.transform != null && hit.transform.IsChildOf(target))
                {
                    continue;
                }

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            return nearest == float.MaxValue ? distance : Mathf.Max(0f, nearest - collisionBuffer);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            distance = Mathf.Max(0f, distance);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            solver.SetPitchLimits(minPitch, maxPitch);
        }
#endif
    }
}
