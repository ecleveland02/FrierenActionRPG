using System;
using Frieren.Core.Persistence;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// Persists a character's vitals and where it was standing.
    /// </summary>
    /// <remarks>
    /// One component owns saving for the whole character rather than each vital doing it itself.
    /// That keeps health and mana as pure gameplay with no knowledge of persistence, and means
    /// adding stamina later is a field in <see cref="VitalsState"/> rather than another
    /// registration.
    ///
    /// It implements <see cref="IPersistentState"/> rather than <c>ISaveable</c>, and identity now
    /// comes from <see cref="SceneObjectId"/> on the same object. That replaces the hand-typed save
    /// key this component used to carry, and it is what finally answers the question left open when
    /// it was written: enemies are spawned at runtime, so their ids have to come from their
    /// spawner. A spawner knows which of its spawns is which; a prefab field cannot.
    ///
    /// Position is optional because it is not always wanted. Reloading should put the player back
    /// where they were; an enemy is better placed by its spawner, so that it cannot be saved into
    /// a wall that has since moved.
    /// </remarks>
    [RequireComponent(typeof(CharacterHealth))]
    [DisallowMultipleComponent]
    public sealed class CharacterPersistence : MonoBehaviour, IPersistentState
    {
        [Serializable]
        private sealed class VitalsState
        {
            public float health;
            public float mana;
            public bool alive = true;
            public bool hasPosition;
            public Vector3 position;
            public float yaw;
        }

        [SerializeField]
        [Tooltip("Save where this character was standing. On for the player; off for anything a spawner places.")]
        private bool persistPosition = true;

        private CharacterHealth health;
        private CharacterMana mana;
        private CharacterMotor motor;

        public string StateKey => "vitals";

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            mana = GetComponent<CharacterMana>();
            motor = GetComponent<CharacterMotor>();
        }

        public string CaptureState()
        {
            return JsonUtility.ToJson(new VitalsState
            {
                health = health != null ? health.Current : 0f,
                mana = mana != null ? mana.Current : 0f,
                alive = health == null || health.IsAlive,
                hasPosition = persistPosition,
                position = transform.position,
                yaw = transform.eulerAngles.y,
            });
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<VitalsState>(json);

            if (state == null)
            {
                return;
            }

            if (health != null)
            {
                health.RestoreState(state.health, state.alive);
            }

            if (mana != null)
            {
                mana.SetCurrent(state.mana);
            }

            if (!persistPosition || !state.hasPosition)
            {
                return;
            }

            var rotation = Quaternion.Euler(0f, state.yaw, 0f);

            if (motor != null)
            {
                // Through the motor, not the transform: a CharacterController caches its own
                // position and would snap straight back on the next move.
                motor.Teleport(state.position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(state.position, rotation);
            }
        }
    }
}
