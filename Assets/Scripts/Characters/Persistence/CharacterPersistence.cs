using System;
using Frieren.Core.Debugging;
using Frieren.Core.Services;
using Frieren.Save;
using Frieren.Save.Serialization;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// Persists a character's vitals across a save and load.
    /// </summary>
    /// <remarks>
    /// One component owns saving for the whole character rather than each vital implementing
    /// <see cref="ISaveable"/> itself. That keeps health and mana as pure gameplay with no
    /// knowledge of the save system, gives the character a single save key instead of one per
    /// component, and means adding stamina later is a field in <see cref="VitalsState"/> rather
    /// than another registration.
    ///
    /// <see cref="saveId"/> must be unique per character. That is fine for the player and for
    /// hand-placed characters; the enemies arriving in Milestone 5 are spawned at runtime and will
    /// need ids derived from their spawner, which is a problem to solve when there is a spawner.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterPersistence : MonoBehaviour, ISaveable
    {
        [Serializable]
        private sealed class VitalsState
        {
            public float health;
            public float mana;
            public bool alive = true;
        }

        [SerializeField]
        [Tooltip("Unique save key for this character. Changing it after saves exist orphans their data.")]
        private string saveId = "character.player";

        private CharacterHealth health;
        private CharacterMana mana;
        private SaveService saveService;

        public string SaveId => saveId;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            mana = GetComponent<CharacterMana>();
        }

        private void Start()
        {
            // Registration happens in Start, not Awake: the character may be spawned before the
            // services exist in an unusual load order, and SaveService restores late registrations
            // itself, so arriving after a load is already handled.
            if (!ServiceLocator.TryGet(out saveService))
            {
                GameLog.Warn(LogChannel.Save,
                    $"{name}: no SaveService, so vitals will not persist. Is the Boot scene loaded?", this);
                return;
            }

            saveService.Register(this);
        }

        private void OnDestroy() => saveService?.Unregister(this);

        public SaveEntry Capture()
        {
            return SaveEntry.Create(saveId, new VitalsState
            {
                health = health != null ? health.Current : 0f,
                mana = mana != null ? mana.Current : 0f,
                alive = health == null || health.IsAlive
            });
        }

        public void Restore(SaveEntry entry)
        {
            if (!entry.TryRead(out VitalsState state))
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

            GameLog.Info(LogChannel.Save,
                $"{name} restored: health {state.health:0.#}, mana {state.mana:0.#}, alive {state.alive}.", this);
        }
    }
}
