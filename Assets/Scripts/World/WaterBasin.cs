using System;
using Frieren.Core.Debugging;
using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.World
{
    public enum BasinState
    {
        Empty = 0,
        Filled = 1,
        Frozen = 2
    }

    /// <summary>
    /// A hollow that holds conjured water, and holds it as ice once it is chilled.
    /// </summary>
    /// <remarks>
    /// Written to make the design pillar concrete rather than to be a puzzle in itself. Filling it
    /// is one spell, freezing it is another, and the frozen surface is a solid the player can stand
    /// on - so "get across the gap" has a two-spell answer that no one scripted, sitting beside
    /// levitating over it and beside mending the walkway. None of those three know about each other.
    ///
    /// Three states rather than two booleans, because "frozen but empty" is not a thing and the
    /// enum makes that unrepresentable.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WaterBasin : MonoBehaviour, IMagicReceiver
    {
        [Header("Filling")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Water that must accumulate before the basin is full.")]
        private float fillThreshold = 20f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Cold needed to freeze a full basin. Chilling an empty one does nothing.")]
        private float freezeThreshold = 15f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Heat that boils a full basin dry, or thaws a frozen one back to water.")]
        private float heatThreshold = 15f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Water lost per second while filling but not yet full. Zero lets it fill over any span.")]
        private float drainPerSecond = 2f;

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Shown when filled. Give it a trigger collider, or none - water is not standable.")]
        private GameObject waterSurface;

        [SerializeField]
        [Tooltip("Shown when frozen. Put the solid, walkable collider on this one.")]
        private GameObject iceSurface;

        private float water;
        private float chill;
        private float warmth;

        public event Action<BasinState, BasinState> StateChanged;

        public BasinState State { get; private set; } = BasinState.Empty;

        public float NormalizedFill => fillThreshold <= 0f ? 0f : Mathf.Clamp01(water / fillThreshold);

        private void Start() => ApplyForm();

        private void Update()
        {
            if (State != BasinState.Empty || water <= 0f || drainPerSecond <= 0f)
            {
                return;
            }

            water = Mathf.Max(0f, water - drainPerSecond * Time.deltaTime);
        }

        public bool ReceiveMagic(in MagicPulse pulse)
        {
            switch (pulse.Element)
            {
                case MagicElement.Water:
                    return AddWater(pulse.Magnitude);
                case MagicElement.Cold:
                    return AddCold(pulse.Magnitude);
                case MagicElement.Heat:
                    return AddHeat(pulse.Magnitude);
                default:
                    return false;
            }
        }

        private bool AddWater(float magnitude)
        {
            if (State != BasinState.Empty)
            {
                return false;
            }

            water += magnitude;

            if (water < fillThreshold)
            {
                return true;
            }

            water = fillThreshold;
            ChangeTo(BasinState.Filled);
            return true;
        }

        private bool AddCold(float magnitude)
        {
            if (State != BasinState.Filled)
            {
                return false;
            }

            chill += magnitude;
            warmth = 0f;

            if (chill < freezeThreshold)
            {
                return true;
            }

            chill = 0f;
            ChangeTo(BasinState.Frozen);
            return true;
        }

        private bool AddHeat(float magnitude)
        {
            if (State == BasinState.Empty)
            {
                return false;
            }

            warmth += magnitude;
            chill = 0f;

            if (warmth < heatThreshold)
            {
                return true;
            }

            warmth = 0f;

            // Ice thaws back to water; water boils away. Two steps rather than one, so undoing a
            // frozen bridge takes as much work as making it.
            if (State == BasinState.Frozen)
            {
                ChangeTo(BasinState.Filled);
            }
            else
            {
                water = 0f;
                ChangeTo(BasinState.Empty);
            }

            return true;
        }

        private void ChangeTo(BasinState next)
        {
            if (State == next)
            {
                return;
            }

            BasinState previous = State;
            State = next;
            ApplyForm();
            GameLog.Info(LogChannel.Interaction, $"{name}: {previous} -> {next}.", this);
            StateChanged?.Invoke(previous, next);
        }

        private void ApplyForm()
        {
            if (waterSurface != null)
            {
                waterSurface.SetActive(State == BasinState.Filled);
            }

            if (iceSurface != null)
            {
                iceSurface.SetActive(State == BasinState.Frozen);
            }
        }
    }
}
