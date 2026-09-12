using Frieren.Characters.Targeting;
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

        [SerializeField]
        [Tooltip("Slides the pivot in camera space. Positive Y lifts it, which drops the character " +
                 "below the middle of the screen so their body stops covering what you are aiming at.")]
        private Vector2 framingOffset = new Vector2(0f, 0.55f);

        [Header("Lock-on")]
        [SerializeField]
        [Tooltip("Seconds for the camera to settle onto a new lock target. Zero snaps.")]
        private float lockTurnSmoothing = 0.12f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("0 keeps the camera's own pitch line, 1 aims straight along the line to the target.")]
        private float lockFocusBias = 0.5f;

        [SerializeField]
        [Tooltip("Extra downward tilt while locked, in degrees. Keeps the ground in frame.")]
        private float lockPitchBias = 8f;

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

        private LockOnTarget lockTarget;

        public Transform Target => target;

        /// <summary>What the camera is holding on, or <c>null</c>.</summary>
        public LockOnTarget LockTarget => lockTarget;

        /// <summary>True while the camera is aiming itself instead of following look input.</summary>
        public bool IsLocked => lockTarget != null && lockTarget.IsLockable;

        /// <summary>
        /// Whether pointer and stick input turn the camera. Something taking over the pointer -
        /// the spell wheel today, a menu later - switches this off while it is up.
        /// </summary>
        public bool LookEnabled { get; set; } = true;

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
        /// Holds the camera on something, or releases it when given <c>null</c>.
        /// </summary>
        /// <remarks>
        /// The rig takes a <see cref="LockOnTarget"/> rather than a bare transform because it needs
        /// the aim point, and a transform's position is on the floor. Locking onto a character's
        /// feet aims the camera at the ground in front of them.
        ///
        /// It does not decide what to lock onto; <c>PlayerTargetLock</c> does that. The rig only
        /// knows how to point at one, which keeps target selection testable on its own and leaves
        /// the camera reusable for a cutscene or a scripted focus that has no player input at all.
        /// </remarks>
        public void SetLockTarget(LockOnTarget newLock)
        {
            lockTarget = newLock != null && newLock.IsLockable ? newLock : null;

            // The player may have been dragging the mouse when the lock was taken or dropped.
            // Keeping that delta would flick the camera on the frame control returns.
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

            // Something else is using the pointer - the spell wheel, and later a menu - or the
            // camera is aiming itself at a lock target. The pending delta is still cleared above,
            // so the camera does not lurch by everything the player moved while it was suspended.
            if (!LookEnabled || IsLocked)
            {
                return;
            }

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
            float deltaTime = Time.deltaTime;

            // Unconditional, because it is also what clears the frame's pointer delta. Skipping it
            // while locked would bank every mouse movement made during the lock and spend them all
            // on the frame it ends.
            ApplyLookThisFrame(deltaTime);

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

            // After the pivot has settled, so the lock aims from where the camera orbits rather
            // than from last frame's position, and before the transform is written.
            AimAtLock(smoothedPivot, deltaTime);

            Vector3 framedPivot = solver.FramedPivot(smoothedPivot, framingOffset);
            float allowedDistance = ResolveDistance(framedPivot);

            transform.SetPositionAndRotation(
                solver.DesiredPosition(framedPivot, allowedDistance),
                solver.Rotation);
        }

        /// <summary>
        /// Turns the orbit toward the lock target, and drops the lock when it stops being one.
        /// </summary>
        /// <remarks>
        /// Yaw comes from the player-to-target line, which puts the two of them on the same column
        /// of the screen. That sounds like a mistake and is the thing that makes a locked camera
        /// readable: with the framing offset pushing the character low, the target lands near the
        /// middle without either of them covering the other.
        ///
        /// Pitch is taken from a point part-way to the target rather than the target itself, so a
        /// tall enemy tilts the camera up somewhat instead of pointing it at the sky, plus a fixed
        /// downward bias so the ground the player is standing on stays in frame.
        /// </remarks>
        private void AimAtLock(Vector3 pivot, float deltaTime)
        {
            if (lockTarget == null)
            {
                return;
            }

            // Checked here rather than only where the lock is set, because a target dies, is
            // destroyed or is disabled without telling anybody.
            if (!lockTarget.IsLockable)
            {
                lockTarget = null;
                return;
            }

            Vector3 focus = Vector3.Lerp(pivot, lockTarget.AimPosition, lockFocusBias);
            Vector2 angles = OrbitCameraSolver.LookAngles(pivot, focus);

            // Exponential rather than a straight lerp on delta time, so the settle takes the same
            // wall-clock time at 30 and 240 frames per second.
            float blend = lockTurnSmoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-deltaTime / lockTurnSmoothing);

            solver.BlendTowards(angles.x, angles.y + lockPitchBias, blend);
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
