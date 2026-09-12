using System.Collections.Generic;
using Frieren.Core.Debugging;
using UnityEngine;

namespace Frieren.Enemies
{
    /// <summary>
    /// Places enemies in a scene from a prefab, and puts them back when asked.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>PlayerSpawner</c> for the same reason: one prefab stays the single definition of
    /// what an enemy is, and scene copies cannot drift from it. It also gives combat a way to start
    /// over during testing without reloading the scene, which matters a great deal when tuning how
    /// hard something hits.
    ///
    /// Deliberately not a wave system, an encounter director or a pool. Those are worth building
    /// when there is a reason to; this is the spawn step every one of them would need underneath.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;

        [SerializeField]
        [Tooltip("Where to spawn. Each transform gets one enemy. Empty means this object's own position.")]
        private List<Transform> spawnPoints = new List<Transform>();

        [SerializeField] private bool spawnOnStart = true;

        private readonly List<GameObject> spawned = new List<GameObject>();

        public IReadOnlyList<GameObject> Spawned => spawned;

        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnAll();
            }
        }

        public void SpawnAll()
        {
            if (enemyPrefab == null)
            {
                GameLog.Error(LogChannel.AI, $"{name}: EnemySpawner has no enemy prefab assigned.", this);
                return;
            }

            if (spawnPoints.Count == 0)
            {
                SpawnAt(transform);
                return;
            }

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i] != null)
                {
                    SpawnAt(spawnPoints[i]);
                }
            }
        }

        public GameObject SpawnAt(Transform point)
        {
            GameObject enemy = Instantiate(enemyPrefab, point.position, point.rotation);
            enemy.name = $"{enemyPrefab.name}_{spawned.Count + 1}";
            spawned.Add(enemy);
            GameLog.Info(LogChannel.AI, $"Spawned {enemy.name} at {point.position}.", this);
            return enemy;
        }

        /// <summary>
        /// Removes every enemy this spawner made and spawns a fresh set. Not named Reset,
        /// which Unity calls by itself from the component's context menu.
        /// </summary>
        public void Respawn()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Destroy(spawned[i]);
                }
            }

            spawned.Clear();
            SpawnAll();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.35f, 0.9f);

            if (spawnPoints.Count == 0)
            {
                Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
                return;
            }

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i] != null)
                {
                    Gizmos.DrawWireCube(spawnPoints[i].position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
                }
            }
        }
#endif
    }
}
