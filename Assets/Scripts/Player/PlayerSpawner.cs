using Frieren.Characters;
using Frieren.Core.Debugging;
using Frieren.Magic;
using Frieren.Player.Cameras;
using UnityEngine;

namespace Frieren.Player
{
    /// <summary>
    /// Places the player in a scene and connects it to the camera.
    /// </summary>
    /// <remarks>
    /// The player is instantiated rather than authored into the scene. Partly that keeps one prefab
    /// as the single definition of what a player is, with no risk of scene copies drifting from it;
    /// mostly it is because respawning, loading a save into a named spawn point, and arriving in a
    /// scene through a specific door all need a spawn step anyway, and they all want this one.
    ///
    /// Wiring is explicit and two-way: the camera is told what to follow, and the player is told
    /// which camera movement is relative to. Neither has to go looking for the other.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [Header("What to spawn")]
        [SerializeField] private GameObject playerPrefab;

        [SerializeField]
        [Tooltip("Where to spawn. Defaults to this object's own transform.")]
        private Transform spawnPoint;

        [Header("Camera")]
        [SerializeField] private OrbitCameraRig cameraRig;

        [SerializeField]
        [Tooltip("Spawn as soon as the scene starts. Turn off when something else decides the moment.")]
        private bool spawnOnStart = true;

        public GameObject SpawnedPlayer { get; private set; }

        private void Start()
        {
            if (spawnOnStart)
            {
                Spawn();
            }
        }

        /// <summary>Spawns the player, or repositions the existing one if there already is one.</summary>
        public GameObject Spawn()
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;

            if (SpawnedPlayer != null)
            {
                Respawn(point);
                return SpawnedPlayer;
            }

            if (playerPrefab == null)
            {
                GameLog.Error(LogChannel.Player, "PlayerSpawner has no player prefab assigned.", this);
                return null;
            }

            SpawnedPlayer = Instantiate(playerPrefab, point.position, point.rotation);
            SpawnedPlayer.name = playerPrefab.name;

            ConnectCamera();
            GameLog.Info(LogChannel.Player, $"Player spawned at {point.position}.", this);
            ReportBody();

            return SpawnedPlayer;
        }

        /// <summary>
        /// Says what the spawned player actually looks like, once, at spawn.
        /// </summary>
        /// <remarks>
        /// "The character is not there" has several causes that look identical from the outside: a
        /// model that failed to import, a visual child that was never instantiated, a mesh scaled to
        /// nothing, a body spawned inside the floor. Each of them is one number away from the
        /// others, and none of them is distinguishable by looking at the screen. One line at spawn
        /// separates them, and costs nothing after the frame it runs on.
        /// </remarks>
        private void ReportBody()
        {
            Renderer[] renderers = SpawnedPlayer.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                GameLog.Error(LogChannel.Player,
                    $"{SpawnedPlayer.name} spawned with no renderers at all. The visual child is " +
                    "missing from the prefab, or its model failed to import.", this);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            int hidden = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);

                if (!renderers[i].enabled || !renderers[i].gameObject.activeInHierarchy)
                {
                    hidden++;
                }
            }

            var animator = SpawnedPlayer.GetComponentInChildren<Animator>();
            string rig = DescribeRig(animator);

            // The placeholder body was a built-in primitive with no rig. Naming that case outright
            // is worth the six lines: an out-of-date prefab and a broken model look identical from
            // the player's chair, and the usual cause is a pull that failed while Unity had the
            // prefab open and quietly kept the old one.
            if (animator == null && UsesPrimitiveMesh(renderers))
            {
                GameLog.Error(LogChannel.Player,
                    "This is the placeholder capsule, not the rigged character. The Player prefab " +
                    "on disk is out of date: run Update.bat, check it reports success, and let " +
                    "Unity finish importing before pressing play.", this);
            }

            GameLog.Info(LogChannel.Player,
                $"{SpawnedPlayer.name} body: {renderers.Length} renderers ({hidden} hidden), " +
                $"bounds size {bounds.size}, centre {bounds.center}, {rig}.", this);

            if (bounds.size.y < 0.2f)
            {
                GameLog.Warn(LogChannel.Player,
                    $"The player's body is {bounds.size.y:0.###}m tall, which is too small to see. " +
                    "Check the model's import scale.", this);
            }
        }

        /// <summary>Moves the existing player back to a spawn point.</summary>
        public void Respawn(Transform point)
        {
            if (SpawnedPlayer == null)
            {
                return;
            }

            if (SpawnedPlayer.TryGetComponent(out CharacterMotor motor))
            {
                // Going through the motor, not the transform: a CharacterController caches its own
                // position and would snap straight back.
                motor.Teleport(point.position, point.rotation);
            }
            else
            {
                SpawnedPlayer.transform.SetPositionAndRotation(point.position, point.rotation);
            }

            if (SpawnedPlayer.TryGetComponent(out CharacterActionLock actionLock))
            {
                actionLock.ForceRelease();
            }
        }

        /// <summary>
        /// Everything about the rig that decides whether a clip can play, in one line.
        /// </summary>
        /// <remarks>
        /// A character that stands still has several causes that look identical: no animator, no
        /// controller, no avatar, an avatar that failed to build, or an avatar that built as generic
        /// when the clips are humanoid. Only the last two are subtle, and both are silent - Unity
        /// reports a failed avatar once at import and never again, so by the time the game is
        /// running there is nothing on screen to distinguish them.
        ///
        /// Retargeting needs the model's avatar and the clips' avatars to both be human. This says
        /// what the model's is; a human avatar here with a character still in bind pose means the
        /// clips are the half that failed.
        /// </remarks>
        private static string DescribeRig(Animator animator)
        {
            if (animator == null)
            {
                return "no Animator";
            }

            if (animator.runtimeAnimatorController == null)
            {
                return "Animator with no controller";
            }

            string controller = animator.runtimeAnimatorController.name;

            if (animator.avatar == null)
            {
                return $"controller {controller}, but NO AVATAR, so no clip can play";
            }

            if (!animator.avatar.isValid)
            {
                return $"controller {controller}, avatar {animator.avatar.name} is INVALID";
            }

            string kind = animator.avatar.isHuman ? "humanoid" : "GENERIC";
            string note = animator.avatar.isHuman
                ? string.Empty
                : " - the clips were converted to Humanoid and cannot retarget onto a generic avatar";

            return $"controller {controller}, {kind} avatar {animator.avatar.name}{note}";
        }

        /// <summary>
        /// Whether the body is one of Unity's built-in primitives, which only the placeholder was.
        /// </summary>
        /// <remarks>
        /// Matched on the mesh name rather than on a component, because the check has to survive
        /// the placeholder being assembled differently than it was; every built-in primitive is
        /// named for its shape and nothing in the real character is.
        /// </remarks>
        private static bool UsesPrimitiveMesh(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                {
                    continue;
                }

                switch (filter.sharedMesh.name)
                {
                    case "Capsule":
                    case "Cube":
                    case "Sphere":
                    case "Cylinder":
                        return true;
                }
            }

            return false;
        }

        private void ConnectCamera()
        {
            if (cameraRig == null)
            {
                GameLog.Warn(LogChannel.Player,
                    "No camera rig assigned; the player will fall back to Camera.main for movement direction.", this);
                return;
            }

            cameraRig.SetTarget(SpawnedPlayer.transform);

            if (SpawnedPlayer.TryGetComponent(out PlayerLocomotion locomotion))
            {
                locomotion.SetCameraReference(cameraRig.transform);
            }

            // Spells aim where the camera looks, not where the body faces. The character turns
            // toward its movement, so body-relative aiming would make it impossible to cast at
            // something while running past it.
            if (SpawnedPlayer.TryGetComponent(out CharacterSpellcaster spellcaster))
            {
                spellcaster.SetAimSource(cameraRig.transform);
            }

            // The wheel borrows the pointer from the camera while it is open, so it needs to know
            // which camera to borrow it from.
            if (SpawnedPlayer.TryGetComponent(out SpellWheelInput spellWheel))
            {
                spellWheel.SetCameraRig(cameraRig);
            }

            // Lock-on measures from the camera, not the body: the player chooses a target by
            // looking at it, so anything else disagrees with what is on screen.
            if (SpawnedPlayer.TryGetComponent(out PlayerTargetLock targetLock))
            {
                targetLock.SetCameraRig(cameraRig);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(point.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
            Gizmos.DrawRay(point.position + Vector3.up, point.forward);
        }
#endif
    }
}
