using System;
using System.Collections.Generic;
using Frieren.Characters;
using Frieren.Core.Debugging;
using Frieren.Core.Persistence;
using UnityEngine;

namespace Frieren.Enemies
{
    /// <summary>
    /// Places enemies in a scene from a prefab, names them so they can be saved, and remembers
    /// which of them are already dead.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>PlayerSpawner</c> for the same reason: one prefab stays the single definition of
    /// what an enemy is, and scene copies cannot drift from it.
    ///
    /// It also settles the identity problem that <c>CharacterPersistence</c> flagged when it was
    /// written and could not solve: a runtime-spawned object has no authored save id, and nothing
    /// but its spawner knows which spawn it is. Each spawn is named "<this spawner's id>.<index>",
    /// which is stable across sessions because it comes from the spawn point's position in the
    /// list rather than from the order things happened to be created.
    ///
    /// Death is recorded by the spawner rather than on the enemy. Restoring a corpse means
    /// restoring a body, an animation state and a disabled collider, all so the player can look at
    /// something they already killed. Not spawning it is the same outcome for none of the work.
    ///
    /// Deliberately not a wave system, an encounter director or a pool. This is the spawn step
    /// every one of those would need underneath.
    /// </remarks>
    [RequireComponent(typeof(SceneObjectId))]
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour, IPersistentState
    {
        [Serializable]
        private sealed class SpawnerState
        {
            public List<int> defeated = new List<int>();
        }

        [SerializeField] private GameObject enemyPrefab;

        [SerializeField]
        [Tooltip("Where to spawn. Each transform gets one enemy. Empty means this object's own position.")]
        private List<Transform> spawnPoints = new List<Transform>();

        [SerializeField] private bool spawnOnStart = true;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly HashSet<int> defeated = new HashSet<int>();
        private SceneObjectId objectId;

        public IReadOnlyList<GameObject> Spawned => spawned;

        /// <summary>Spawn indices whose enemy has been killed and should not come back.</summary>
        public IReadOnlyCollection<int> Defeated => defeated;

        public string StateKey => "spawner";

        private void Awake() => objectId = GetComponent<SceneObjectId>();

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

            int count = Mathf.Max(1, spawnPoints.Count);

            for (int i = 0; i < count; i++)
            {
                if (defeated.Contains(i))
                {
                    continue;
                }

                Transform point = spawnPoints.Count == 0 ? transform : spawnPoints[i];

                if (point != null)
                {
                    SpawnAt(point, i);
                }
            }
        }

        public GameObject SpawnAt(Transform point, int index)
        {
            GameObject enemy = Instantiate(enemyPrefab, point.position, point.rotation);
            enemy.name = $"{enemyPrefab.name}_{index + 1}";

            // Named between Awake and Start, so its own PersistentObject has an id by the time it
            // registers. The index is a parameter rather than a captured loop variable, so each
            // handler closes over its own copy.
            if (enemy.TryGetComponent(out SceneObjectId spawnedId))
            {
                spawnedId.Assign($"{SpawnerId}.{index}");
            }

            if (enemy.TryGetComponent(out CharacterHealth health))
            {
                health.Died += _ => OnSpawnDefeated(index);
            }

            spawned.Add(enemy);
            GameLog.Info(LogChannel.AI, $"Spawned {enemy.name} at {point.position}.", this);
            return enemy;
        }

        private string SpawnerId => objectId != null && objectId.HasId ? objectId.Id : name;

        private void OnSpawnDefeated(int index) => defeated.Add(index);

        /// <summary>
        /// Removes every enemy this spawner made and spawns a fresh set, forgetting the dead. Not
        /// named Reset, which Unity calls by itself from the component's context menu.
        /// </summary>
        public void Respawn()
        {
            Clear();
            defeated.Clear();
            SpawnAll();
        }

        private void Clear()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Destroy(spawned[i]);
                }
            }

            spawned.Clear();
        }

        public string CaptureState()
        {
            var state = new SpawnerState();
            state.defeated.AddRange(defeated);
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<SpawnerState>(json);

            if (state?.defeated == null)
            {
                return;
            }

            defeated.Clear();

            for (int i = 0; i < state.defeated.Count; i++)
            {
                defeated.Add(state.defeated[i]);
            }

            // Loading mid-fight should not leave the enemies from before the load standing.
            Clear();
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
