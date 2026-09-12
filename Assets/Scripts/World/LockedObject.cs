using System;
using Frieren.Core.Debugging;
using Frieren.Core.Interaction;
using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// A door, chest or gate that stays shut until something undoes the lock.
    /// </summary>
    /// <remarks>
    /// The clearest case for the multiple-solutions pillar. Unbinding magic picks it; enough Force
    /// smashes it; a key opens it by hand. All three run through the same
    /// <see cref="Unlock(GameObject)"/> path, so the door itself has no idea which one happened and
    /// a fourth answer costs nothing to add.
    ///
    /// Being both <see cref="IMagicReceiver"/> and <see cref="IInteractable"/> is deliberate rather
    /// than a merge of the two: the magic side changes whether it is locked, the interaction side
    /// changes whether it is open. Keeping them apart is what allows a door to be unlocked but
    /// still shut.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LockedObject : MonoBehaviour, IMagicReceiver, IInteractable
    {
        [Header("Lock")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Unbinding needed to pick the lock.")]
        private float unbindingThreshold = 15f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Force needed to break it instead. Zero means it cannot be forced.")]
        private float forceThreshold = 40f;

        [Header("Opening")]
        [SerializeField]
        [Tooltip("Disabled when it opens. The blocking collider and the closed visual go here.")]
        private GameObject closedForm;

        [SerializeField]
        [Tooltip("Enabled when it opens, if there is anything to show.")]
        private GameObject openForm;

        [SerializeField] private Transform interactionPoint;

        [SerializeField] private string lockedPrompt = "Locked";

        [SerializeField] private string openPrompt = "Open";

        [SerializeField]
        [Tooltip("Whether it swings open by itself the moment the lock gives way.")]
        private bool openOnUnlock = true;

        private float pickProgress;

        public event Action<GameObject> Unlocked;

        public event Action Opened;

        public bool IsLocked { get; private set; } = true;

        public bool IsOpen { get; private set; }

        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;

        public string InteractionPrompt => IsLocked ? lockedPrompt : openPrompt;

        private void Start() => ApplyForm();

        // A locked door is still worth prompting about; the prompt is how the player learns it is
        // locked at all.
        public bool CanInteract(GameObject actor) => !IsOpen;

        public void Interact(GameObject actor)
        {
            if (IsLocked)
            {
                GameLog.Info(LogChannel.Interaction, $"{name} is locked.", this);
                return;
            }

            Open();
        }

        public bool ReceiveMagic(in MagicPulse pulse)
        {
            if (!IsLocked)
            {
                return false;
            }

            switch (pulse.Element)
            {
                case MagicElement.Unbinding:
                    pickProgress += pulse.Magnitude;

                    if (pickProgress < unbindingThreshold)
                    {
                        return true;
                    }

                    Unlock(pulse.Source);
                    return true;

                case MagicElement.Force:
                    if (forceThreshold <= 0f || pulse.Magnitude < forceThreshold)
                    {
                        return false;
                    }

                    GameLog.Info(LogChannel.Interaction, $"{name} was forced open.", this);
                    Unlock(pulse.Source);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Undoes the lock, whatever undid it. Keys, magic and scripted events all land here.</summary>
        public bool Unlock(GameObject source)
        {
            if (!IsLocked)
            {
                return false;
            }

            IsLocked = false;
            pickProgress = 0f;
            GameLog.Info(LogChannel.Interaction, $"{name} was unlocked.", this);
            Unlocked?.Invoke(source);

            if (openOnUnlock)
            {
                Open();
            }

            return true;
        }

        public bool Open()
        {
            if (IsOpen || IsLocked)
            {
                return false;
            }

            IsOpen = true;
            ApplyForm();
            Opened?.Invoke();
            return true;
        }

        private void ApplyForm()
        {
            if (closedForm != null)
            {
                closedForm.SetActive(!IsOpen);
            }

            if (openForm != null)
            {
                openForm.SetActive(IsOpen);
            }
        }
    }
}
