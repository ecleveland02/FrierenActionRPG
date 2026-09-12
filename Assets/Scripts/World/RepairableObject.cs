using System;
using Frieren.Core.Debugging;
using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// Something broken that mending magic can put back together: a collapsed walkway, a cracked
    /// pillar, a shattered mechanism.
    /// </summary>
    /// <remarks>
    /// Swaps two child objects rather than deforming a mesh. A "broken" object and an "intact"
    /// object are far cheaper to author with placeholder geometry, and the runtime contract - one
    /// element in, one state change out - is identical to whatever the final art does.
    ///
    /// Reacts to Restoration only. It has no idea which spell sent it, which is what lets a future
    /// healing spell, a repair rune or an NPC's magic all mend the same walkway.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RepairableObject : MonoBehaviour, IMagicReceiver
    {
        [Header("Mending")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Restoration that must accumulate before it is whole again.")]
        private float repairThreshold = 20f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Progress lost per second, so a half-finished repair does not sit there forever.")]
        private float decayPerSecond = 3f;

        [SerializeField]
        [Tooltip("Whether it can be broken again after mending. Off makes repair permanent.")]
        private bool canBreakAgain = true;

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Shown while broken. Disabled once mended.")]
        private GameObject brokenForm;

        [SerializeField]
        [Tooltip("Shown once mended. Disabled while broken. Put the walkable collider on this.")]
        private GameObject intactForm;

        private float progress;

        /// <summary>Raised when it becomes whole.</summary>
        public event Action Repaired;

        /// <summary>Raised when it is broken again.</summary>
        public event Action Broken;

        /// <summary>Raised as progress changes, normalised 0-1, for prompts and effects.</summary>
        public event Action<float> ProgressChanged;

        [field: SerializeField]
        [field: Tooltip("Whether it starts whole. A repairable that starts intact is a thing to break.")]
        public bool IsIntact { get; private set; }

        public float NormalizedProgress => repairThreshold <= 0f ? 0f : Mathf.Clamp01(progress / repairThreshold);

        private void Start() => ApplyForm();

        private void Update()
        {
            if (IsIntact || progress <= 0f || decayPerSecond <= 0f)
            {
                return;
            }

            progress = Mathf.Max(0f, progress - decayPerSecond * Time.deltaTime);
            ProgressChanged?.Invoke(NormalizedProgress);
        }

        public bool ReceiveMagic(in MagicPulse pulse)
        {
            if (pulse.Element != MagicElement.Restoration)
            {
                return false;
            }

            if (IsIntact)
            {
                return false;
            }

            progress += pulse.Magnitude;
            ProgressChanged?.Invoke(NormalizedProgress);

            if (progress < repairThreshold)
            {
                // Partial progress is still feedback worth reporting, or the player cannot tell a
                // slow repair from an ineffective one.
                return true;
            }

            progress = 0f;
            IsIntact = true;
            ApplyForm();
            GameLog.Info(LogChannel.Interaction, $"{name} was mended.", this);
            Repaired?.Invoke();
            return true;
        }

        /// <summary>Breaks it again. For hazards, enemies and scripted events.</summary>
        public bool Break()
        {
            if (!IsIntact || !canBreakAgain)
            {
                return false;
            }

            IsIntact = false;
            progress = 0f;
            ApplyForm();
            ProgressChanged?.Invoke(0f);
            Broken?.Invoke();
            return true;
        }

        private void ApplyForm()
        {
            if (brokenForm != null)
            {
                brokenForm.SetActive(!IsIntact);
            }

            if (intactForm != null)
            {
                intactForm.SetActive(IsIntact);
            }
        }
    }
}
