using System.Collections.Generic;
using Frieren.Characters.Targeting;
using Frieren.Core;
using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Player.Cameras;
using UnityEngine;

namespace Frieren.Player
{
    /// <summary>
    /// Holds the camera and the character on one target until the player lets go or it dies.
    /// </summary>
    /// <remarks>
    /// Three separate things want to know about a lock and they are kept apart on purpose. This
    /// component decides <em>what</em> is locked; <see cref="OrbitCameraRig"/> knows how to point
    /// at it; <see cref="LockOnPicker"/> knows how to choose between candidates and is plain
    /// functions over positions. Only the middle one touches a transform and only the last one is
    /// worth unit testing, which is the split that makes a camera feature debuggable at all.
    ///
    /// Aim needs no special case. Spells already fire along the camera's forward, and while locked
    /// the camera is pointing at the target, so casting at what you locked onto simply works. That
    /// was the reason to route aim through the camera rather than the body in the first place.
    ///
    /// Flicking the look stick or mouse sideways switches targets rather than turning the camera,
    /// because turning is exactly what the player gave up by locking on. The threshold is on
    /// accumulated travel rather than instantaneous speed, so a slow deliberate push works as well
    /// as a fast one and hand tremor does not.
    /// </remarks>
    [RequireComponent(typeof(PlayerLocomotion))]
    [DisallowMultipleComponent]
    public sealed class PlayerTargetLock : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;

        [Header("Acquisition")]
        [SerializeField]
        [Tooltip("Furthest a target can be when acquiring.")]
        private float acquireRange = 22f;

        [SerializeField]
        [Tooltip("Degrees off the camera's forward a target may sit and still be offered.")]
        private float acquireAngle = 65f;

        [SerializeField]
        [Tooltip("How many degrees of angle one metre of distance is worth when scoring. Higher " +
                 "prefers the nearer target, lower prefers the one you are looking straight at.")]
        private float distanceWeight = 1.5f;

        [Header("Holding")]
        [SerializeField]
        [Tooltip("The lock drops past this. Larger than the acquire range so it does not flicker " +
                 "off the moment you back up a step.")]
        private float breakRange = 30f;

        [SerializeField]
        [Tooltip("Require line of sight to hold a lock.")]
        private bool requireLineOfSight = true;

        [SerializeField]
        [Tooltip("Seconds the target may stay hidden before the lock drops. Covers walking past a pillar.")]
        private float sightGrace = 1.2f;

        [Header("Switching")]
        [SerializeField]
        [Tooltip("Pixels of accumulated mouse travel needed to flick to the next target.")]
        private float pointerSwitchThreshold = 260f;

        [SerializeField]
        [Tooltip("Seconds of full stick deflection needed to flick to the next target.")]
        private float stickSwitchThreshold = 0.22f;

        [SerializeField]
        [Tooltip("How fast banked travel drains away when the player stops pushing, per second.")]
        private float switchDecay = 3f;

        private PlayerLocomotion locomotion;
        private OrbitCameraRig cameraRig;

        // Reused every acquisition. Building a fresh list each time would allocate on a button
        // press, which is the one place a stutter is guaranteed to be noticed.
        private readonly List<LockOnTarget> candidates = new List<LockOnTarget>();
        private readonly List<Vector3> candidatePoints = new List<Vector3>();

        private float switchTravel;
        private float hiddenSince = -1f;

        /// <summary>The target being held, or <c>null</c>.</summary>
        public LockOnTarget Current { get; private set; }

        public bool IsLocked => Current != null;

        private void Awake()
        {
            locomotion = GetComponent<PlayerLocomotion>();

            if (inputReader == null)
            {
                GameLog.Error(LogChannel.Player,
                    $"{name}: PlayerTargetLock has no InputReader, so lock-on will never fire.", this);
            }
        }

        /// <summary>Told by the spawner, the same way the wheel and the spellcaster are.</summary>
        public void SetCameraRig(OrbitCameraRig rig) => cameraRig = rig;

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.LockOnPerformed += Toggle;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.LockOnPerformed -= Toggle;
            }

            // A disabled component must not leave the camera holding a target nothing will release.
            Release();
        }

        public void Toggle()
        {
            if (IsLocked)
            {
                Release();
                return;
            }

            Acquire();
        }

        /// <summary>Locks onto the best candidate, or does nothing when there is none.</summary>
        public void Acquire()
        {
            CollectCandidates();

            if (candidates.Count == 0)
            {
                return;
            }

            Vector3 eye = EyePosition();
            Vector3 forward = cameraRig != null ? cameraRig.transform.forward : transform.forward;
            int best = LockOnPicker.Best(candidatePoints, eye, forward, acquireRange, acquireAngle, distanceWeight);

            if (best < 0)
            {
                return;
            }

            Adopt(candidates[best]);
        }

        public void Release()
        {
            if (Current == null)
            {
                return;
            }

            Current = null;
            switchTravel = 0f;
            hiddenSince = -1f;

            if (cameraRig != null)
            {
                cameraRig.SetLockTarget(null);
            }

            if (locomotion != null)
            {
                locomotion.FaceTarget = null;
            }
        }

        private void Adopt(LockOnTarget target)
        {
            Current = target;
            switchTravel = 0f;
            hiddenSince = -1f;

            if (cameraRig != null)
            {
                cameraRig.SetLockTarget(target);
            }

            if (locomotion != null)
            {
                // The body faces what the camera is holding, so backing away from an enemy is a
                // backward step rather than a turn and a run.
                locomotion.FaceTarget = target.transform;
            }
        }

        private void Update()
        {
            if (!IsLocked)
            {
                return;
            }

            if (!StillValid())
            {
                Release();
                return;
            }

            // The transform is followed rather than re-read from Current every frame elsewhere, so
            // keep locomotion pointed at it in case the target re-parented or was pooled.
            if (locomotion != null && locomotion.FaceTarget != Current.transform)
            {
                locomotion.FaceTarget = Current.transform;
            }

            TrackSwitchFlick();
        }

        private bool StillValid()
        {
            if (Current == null || !Current.IsLockable)
            {
                return false;
            }

            Vector3 eye = EyePosition();

            if ((Current.AimPosition - eye).sqrMagnitude > breakRange * breakRange)
            {
                return false;
            }

            if (!requireLineOfSight)
            {
                return true;
            }

            if (HasLineOfSight(eye, Current.AimPosition))
            {
                hiddenSince = -1f;
                return true;
            }

            // A grace period rather than an instant drop. Fighting beside a pillar otherwise
            // breaks the lock several times a second, which is worse than no lock at all.
            if (hiddenSince < 0f)
            {
                hiddenSince = Time.time;
            }

            return Time.time - hiddenSince < sightGrace;
        }

        private static bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;

            if (distance <= 0.01f)
            {
                return true;
            }

            return !Physics.Raycast(from, offset / distance, distance, GameLayers.SolidMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Banks sideways look input and switches target once enough of it has piled up.
        /// </summary>
        /// <remarks>
        /// Mouse and stick are counted in their own units and against their own thresholds, and
        /// they have to be: a mouse reports pixels moved this frame, a stick reports how far it is
        /// pushed. One shared number cannot mean both. Sharing one anyway is how you get a lock
        /// that switches target every time the player breathes on the mouse.
        ///
        /// Unscaled time throughout, because the spell wheel slows the clock and a flick should
        /// take as long as it takes whatever the game is doing.
        /// </remarks>
        private void TrackSwitchFlick()
        {
            if (inputReader == null)
            {
                return;
            }

            // The wheel switches this off while it owns the pointer. Without the check, choosing a
            // spell would also be flicking through targets with the same mouse movement.
            if (cameraRig != null && !cameraRig.LookEnabled)
            {
                switchTravel = 0f;
                return;
            }

            float horizontal = inputReader.LookInput.x;
            bool pointer = inputReader.LookIsPointerDelta;
            float step = pointer ? horizontal : horizontal * Time.unscaledDeltaTime;
            float threshold = pointer ? pointerSwitchThreshold : stickSwitchThreshold;

            // Sign changes mean the player reversed direction, so travel banked toward the old side
            // is no longer evidence of anything.
            if (step * switchTravel < 0f)
            {
                switchTravel = 0f;
            }

            switchTravel += step;

            // Drain when nothing is being pushed, so a slow drift across a whole fight never adds
            // up to a flick the player did not make.
            if (Mathf.Abs(step) <= 0.0001f)
            {
                switchTravel = Mathf.MoveTowards(switchTravel, 0f,
                    threshold * switchDecay * Time.unscaledDeltaTime);
            }

            if (Mathf.Abs(switchTravel) < threshold)
            {
                return;
            }

            SwitchTarget(Mathf.Sign(switchTravel));
            switchTravel = 0f;
        }

        private void SwitchTarget(float sign)
        {
            CollectCandidates();

            int current = candidates.IndexOf(Current);

            if (current < 0)
            {
                return;
            }

            Vector3 eye = EyePosition();
            Vector3 right = cameraRig != null ? cameraRig.transform.right : transform.right;
            int next = LockOnPicker.NextInDirection(candidatePoints, current, eye, right, sign, breakRange);

            if (next >= 0)
            {
                Adopt(candidates[next]);
            }
        }

        /// <summary>
        /// Where targeting is measured from. The camera, not the body: the player picks a target by
        /// looking at it, so anything else disagrees with what is on screen.
        /// </summary>
        private Vector3 EyePosition() =>
            cameraRig != null ? cameraRig.transform.position : transform.position + Vector3.up * 1.4f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle reticleStyle;

        /// <summary>
        /// A placeholder reticle. Replaced the day there is a HUD; until then, a lock you cannot
        /// see is a lock you cannot tell apart from a camera bug.
        /// </summary>
        private void OnGUI()
        {
            if (!IsLocked || cameraRig == null)
            {
                return;
            }

            UnityEngine.Camera view = cameraRig.GetComponent<UnityEngine.Camera>();

            if (view == null)
            {
                return;
            }

            Vector3 screen = view.WorldToScreenPoint(Current.AimPosition);

            // Negative z means the point is behind the camera, where WorldToScreenPoint still
            // returns coordinates and they are mirrored nonsense.
            if (screen.z <= 0f)
            {
                return;
            }

            reticleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
            };

            float x = screen.x;
            float y = Screen.height - screen.y;

            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.55f, 0.35f);
            GUI.Label(new Rect(x - 12f, y - 12f, 24f, 24f), "[ ]", reticleStyle);
            GUI.Label(new Rect(x - 80f, y + 10f, 160f, 20f), Current.DisplayName, reticleStyle);
            GUI.color = previous;
        }
#endif

        private void CollectCandidates()
        {
            candidates.Clear();
            candidatePoints.Clear();

            IReadOnlyList<LockOnTarget> active = LockOnTarget.Active;

            for (int i = 0; i < active.Count; i++)
            {
                LockOnTarget target = active[i];

                if (target == null || !target.IsLockable || target.transform.IsChildOf(transform))
                {
                    continue;
                }

                candidates.Add(target);
                candidatePoints.Add(target.AimPosition);
            }
        }
    }
}
