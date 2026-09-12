using System;
using Frieren.Core.Debugging;
using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// Something that catches fire when heated enough, and goes out when chilled.
    /// </summary>
    /// <remarks>
    /// The reference implementation of <see cref="IMagicReceiver"/>. Note what it does not know: no
    /// spell name appears anywhere in it. It reacts to Heat and Cold, so any spell that emits Heat
    /// lights it and any spell that emits Cold puts it out - including spells that do not exist yet.
    /// That is the whole point of the contract, and it is why "burn the barricade" will have several
    /// answers rather than one scripted one.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FlammableObject : MonoBehaviour, IMagicReceiver
    {
        [Header("Ignition")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Heat that must accumulate before it catches. Compare against what your spells emit.")]
        private float ignitionThreshold = 10f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How long it burns once alight, in seconds.")]
        private float burnDuration = 6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Heat lost per second while not alight, so repeated weak hits do not eventually add up.")]
        private float coolingPerSecond = 4f;

        [Header("Consequences")]
        [SerializeField]
        [Tooltip("Disable this object once it has finished burning, as a burnt barricade should stop blocking.")]
        private bool disableWhenConsumed = true;

        [Header("Placeholder visuals")]
        [SerializeField] private Renderer tintTarget;

        [SerializeField] private Color coldColour = new Color(0.55f, 0.42f, 0.3f);

        [SerializeField] private Color hotColour = new Color(0.95f, 0.45f, 0.15f);

        [SerializeField] private Color ashColour = new Color(0.18f, 0.16f, 0.16f);

        private BurnState state;
        private MaterialPropertyBlock propertyBlock;
        private int baseColorId;
        private int colorId;

        /// <summary>Raised when it catches fire.</summary>
        public event Action Ignited;

        /// <summary>Raised when a fire is put out before it finished.</summary>
        public event Action Extinguished;

        /// <summary>Raised when it has burnt away completely.</summary>
        public event Action Consumed;

        public bool IsBurning => state != null && state.IsBurning;

        public bool IsConsumed => state != null && state.IsConsumed;

        private void Awake()
        {
            state = new BurnState(ignitionThreshold, burnDuration, coolingPerSecond);
            propertyBlock = new MaterialPropertyBlock();
            baseColorId = Shader.PropertyToID("_BaseColor");
            colorId = Shader.PropertyToID("_Color");

            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>();
            }

            ApplyTint();
        }

        private void Update()
        {
            if (state.Tick(Time.deltaTime))
            {
                GameLog.Info(LogChannel.Interaction, $"{name} burnt out.", this);
                Consumed?.Invoke();
                ApplyTint();

                if (disableWhenConsumed)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            ApplyTint();
        }

        public bool ReceiveMagic(in MagicPulse pulse)
        {
            switch (pulse.Element)
            {
                case MagicElement.Heat:
                    if (state.AddHeat(pulse.Magnitude))
                    {
                        GameLog.Info(LogChannel.Interaction, $"{name} caught fire.", this);
                        Ignited?.Invoke();
                        return true;
                    }

                    // Warming something that has not caught yet is still an effect worth reporting,
                    // so the player gets feedback that they are making progress.
                    return !state.IsConsumed;

                case MagicElement.Cold:
                case MagicElement.Water:
                    if (state.AddCold(pulse.Magnitude))
                    {
                        GameLog.Info(LogChannel.Interaction, $"{name} was put out.", this);
                        Extinguished?.Invoke();
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        private void ApplyTint()
        {
            if (tintTarget == null)
            {
                return;
            }

            Color colour = state.IsConsumed ? ashColour
                : state.IsBurning ? Color.Lerp(hotColour, ashColour, state.BurnProgress)
                : Color.Lerp(coldColour, hotColour, state.HeatProgress);

            tintTarget.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(baseColorId, colour);
            propertyBlock.SetColor(colorId, colour);
            tintTarget.SetPropertyBlock(propertyBlock);
        }
    }
}
