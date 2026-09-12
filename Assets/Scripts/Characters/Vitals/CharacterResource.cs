using System;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// Shared behaviour for a character's regenerating pools.
    /// </summary>
    /// <remarks>
    /// Health and mana are the same problem twice: a bounded value, a maximum that comes from
    /// stats, regeneration that pauses for a moment after the pool is used, and an event whenever
    /// it moves. Stamina will be the third. The differences - death, all-or-nothing spending - live
    /// in the subclasses, which is where they belong.
    ///
    /// Tuning comes entirely from <see cref="CharacterStats"/>. There are deliberately no
    /// serialized numbers on this component: two places to set a maximum is one place too many, and
    /// the one that loses is always the one someone forgot to update.
    /// </remarks>
    [RequireComponent(typeof(CharacterStats))]
    public abstract class CharacterResource : MonoBehaviour
    {
        private float regenBlockedUntil;
        private ResourcePool pool;
        private CharacterStats stats;

        /// <summary>
        /// The backing pool, created on first use.
        /// </summary>
        /// <remarks>
        /// Lazy rather than built in <c>Awake</c> for two reasons. Unity does not call <c>Awake</c>
        /// in edit mode, so anything created there is unreachable from an edit-mode test; and a
        /// character assembled by a spawner may have its stats set after its components exist, in
        /// which case an eagerly built pool would have taken its maximum from an empty definition.
        /// </remarks>
        protected ResourcePool Pool => pool ??= new ResourcePool(MaxFromStats);

        protected CharacterStats Stats
        {
            get
            {
                if (stats == null)
                {
                    stats = GetComponent<CharacterStats>();
                }

                return stats;
            }
        }

        public float Current => Pool.Current;

        public float Max => Pool.Max;

        /// <summary>0 when empty, 1 when full. What a bar should read.</summary>
        public float Normalized => Pool.Normalized;

        public bool IsFull => Pool.IsFull;

        public bool IsDepleted => Pool.IsDepleted;

        /// <summary>Raised whenever the value moves, as (current, max).</summary>
        public event Action<float, float> Changed;

        /// <summary>Raised when the pool reaches zero, once per emptying.</summary>
        public event Action Emptied;

        protected abstract float MaxFromStats { get; }

        protected abstract float RegenPerSecond { get; }

        protected abstract float RegenDelaySeconds { get; }

        /// <summary>Whether regeneration should run at all right now.</summary>
        protected virtual bool CanRegenerate => true;

        protected virtual void Awake()
        {
            // Force initialisation now so it happens at a predictable point in play, rather than
            // on whichever component happens to read the pool first.
            _ = Pool;
        }

        protected virtual void OnEnable()
        {
            if (Stats != null)
            {
                Stats.Changed += SyncMaxFromStats;
            }
        }

        protected virtual void OnDisable()
        {
            if (Stats != null)
            {
                Stats.Changed -= SyncMaxFromStats;
            }
        }

        protected virtual void Update() => Regenerate(Time.deltaTime);

        /// <summary>Adds to the pool. Returns how much actually went in.</summary>
        public float Replenish(float amount)
        {
            float added = Pool.Add(amount);

            if (added > 0f)
            {
                RaiseChanged();
            }

            return added;
        }

        /// <summary>
        /// Refills to maximum without triggering the post-use regeneration delay. For respawning
        /// and for restoring a save.
        /// </summary>
        public void FillToMax()
        {
            Pool.Fill();
            regenBlockedUntil = 0f;
            RaiseChanged();
        }

        /// <summary>Sets the value directly, clamped. Used when restoring saved state.</summary>
        public void SetCurrent(float value)
        {
            Pool.SetCurrent(value);
            RaiseChanged();
        }

        /// <summary>Re-reads the maximum from stats, keeping the current value's proportion.</summary>
        public void SyncMaxFromStats()
        {
            Pool.SetMax(MaxFromStats, scaleCurrent: true);
            RaiseChanged();
        }

        /// <summary>Takes from the pool and starts the regeneration delay. Returns what came out.</summary>
        protected float Drain(float amount)
        {
            float removed = Pool.Remove(amount);

            if (removed <= 0f)
            {
                return 0f;
            }

            BlockRegeneration();
            RaiseChanged();

            if (Pool.IsDepleted)
            {
                Emptied?.Invoke();
            }

            return removed;
        }

        /// <summary>All-or-nothing withdrawal, for costs that must not be part-paid.</summary>
        protected bool TryDrainExactly(float amount)
        {
            if (!Pool.TryRemove(amount))
            {
                return false;
            }

            BlockRegeneration();
            RaiseChanged();

            if (Pool.IsDepleted)
            {
                Emptied?.Invoke();
            }

            return true;
        }

        protected void BlockRegeneration() => regenBlockedUntil = Time.time + RegenDelaySeconds;

        protected void RaiseChanged() => Changed?.Invoke(Current, Max);

        private void Regenerate(float deltaTime)
        {
            if (!CanRegenerate || RegenPerSecond <= 0f || Pool.IsFull || Time.time < regenBlockedUntil)
            {
                return;
            }

            if (Pool.Add(RegenPerSecond * deltaTime) > 0f)
            {
                RaiseChanged();
            }
        }
    }
}
