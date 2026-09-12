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

            return SpawnedPlayer;
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
