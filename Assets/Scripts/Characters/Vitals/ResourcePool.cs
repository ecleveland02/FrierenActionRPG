using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// A bounded quantity with a current and a maximum: health, mana, and later stamina or shields.
    /// </summary>
    /// <remarks>
    /// Plain C# on purpose. Clamping, proportional rescaling when the maximum changes, and
    /// all-or-nothing spending are the parts that are easy to get subtly wrong and impossible to
    /// confirm by looking at a health bar, so they live somewhere a test can reach them.
    ///
    /// Every mutator returns the amount that actually changed rather than the amount requested.
    /// Callers almost always want the real figure - floating combat text showing "50" when only 8
    /// damage landed is a bug, and so is a spell firing when the mana was not really there.
    /// </remarks>
    public sealed class ResourcePool
    {
        private float max;
        private float current;

        public ResourcePool(float max, float? current = null)
        {
            this.max = Mathf.Max(0f, max);
            this.current = Mathf.Clamp(current ?? this.max, 0f, this.max);
        }

        public float Max => max;

        public float Current => current;

        /// <summary>0 when empty, 1 when full. Zero for a pool with no maximum, not a divide by zero.</summary>
        public float Normalized => max <= 0f ? 0f : current / max;

        public bool IsFull => current >= max;

        public bool IsDepleted => current <= 0f;

        /// <summary>Adds up to <paramref name="amount"/>, and returns how much actually went in.</summary>
        public float Add(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float before = current;
            current = Mathf.Min(max, current + amount);
            return current - before;
        }

        /// <summary>Removes up to <paramref name="amount"/>, and returns how much actually came out.</summary>
        public float Remove(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float before = current;
            current = Mathf.Max(0f, current - amount);
            return before - current;
        }

        /// <summary>
        /// Removes the full amount or nothing at all. This is what spending a spell's mana cost
        /// needs: a half-paid spell is worse than an uncast one.
        /// </summary>
        public bool TryRemove(float amount)
        {
            if (amount < 0f || current < amount)
            {
                return false;
            }

            current -= amount;
            return true;
        }

        /// <summary>
        /// Changes the maximum.
        /// </summary>
        /// <param name="scaleCurrent">
        /// When true the current value keeps its proportion, which is what a temporary buff wants -
        /// a character at half health stays at half health. When false it keeps its absolute value
        /// and is clamped, which is what permanent progression wants: gaining maximum health should
        /// not heal you.
        /// </param>
        public void SetMax(float newMax, bool scaleCurrent = false)
        {
            newMax = Mathf.Max(0f, newMax);

            if (scaleCurrent && max > 0f)
            {
                current = Normalized * newMax;
            }

            max = newMax;
            current = Mathf.Clamp(current, 0f, max);
        }

        public void SetCurrent(float value) => current = Mathf.Clamp(value, 0f, max);

        public void Fill() => current = max;

        public void Empty() => current = 0f;

        public override string ToString() => $"{current:0.#}/{max:0.#}";
    }
}
