using System;
using System.Collections.Generic;
using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Core.Interaction;
using UnityEngine;

namespace Frieren.Player
{
    /// <summary>
    /// Finds the interactable the player most likely means, and acts on it when Interact fires.
    /// </summary>
    /// <remarks>
    /// Scanning is throttled rather than run every frame: the result only has to be correct at the
    /// moment the player presses a button, and an overlap query per frame per character is exactly
    /// the kind of cost that is invisible now and awkward to remove once a dozen NPCs do it too.
    /// The scan uses the non-allocating overlap API with a fixed buffer for the same reason.
    ///
    /// <see cref="CurrentChanged"/> is what a prompt UI will subscribe to. Nothing listens yet;
    /// the event is here so the UI milestone does not have to modify this class.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int MaxCandidates = 16;

        [Header("Input")]
        [SerializeField] private InputReader inputReader;

        [Header("Probe")]
        [SerializeField]
        [Tooltip("Origin of the search. Defaults to this transform, raised by the vertical offset.")]
        private Transform probeOrigin;

        [SerializeField] private float verticalOffset = 1f;

        [SerializeField] private float maxDistance = 2.5f;

        [SerializeField]
        [Tooltip("How far off-centre an interactable may be and still be offered, in degrees.")]
        private float maxAngle = 90f;

        [SerializeField]
        [Tooltip("Relative weight of aim over proximity. Above 1 favours what the player faces.")]
        private float angleWeight = 1.5f;

        [SerializeField] private LayerMask interactableLayers = ~0;

        [SerializeField]
        [Tooltip("Seconds between scans. Zero scans every frame.")]
        private float scanInterval = 0.1f;

        private readonly Collider[] overlapBuffer = new Collider[MaxCandidates];
        private readonly List<IInteractable> candidates = new List<IInteractable>(MaxCandidates);
        private readonly List<Vector3> candidatePositions = new List<Vector3>(MaxCandidates);

        private float nextScanTime;

        /// <summary>The interactable currently offered, or null.</summary>
        public IInteractable Current { get; private set; }

        /// <summary>Raised when <see cref="Current"/> changes, including to null.</summary>
        public event Action<IInteractable> CurrentChanged;

        private Vector3 Origin => probeOrigin != null
            ? probeOrigin.position
            : transform.position + Vector3.up * verticalOffset;

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.InteractPerformed += OnInteractPressed;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.InteractPerformed -= OnInteractPressed;
            }

            SetCurrent(null);
        }

        private void Update()
        {
            if (Time.time < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.time + Mathf.Max(0f, scanInterval);
            Scan();
        }

        private void Scan()
        {
            candidates.Clear();
            candidatePositions.Clear();

            int found = Physics.OverlapSphereNonAlloc(Origin, maxDistance, overlapBuffer,
                interactableLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < found; i++)
            {
                Collider collider = overlapBuffer[i];

                if (collider == null)
                {
                    continue;
                }

                // GetComponentInParent so a collider can sit on a child of the interactable.
                var interactable = collider.GetComponentInParent<IInteractable>();

                if (interactable == null || !interactable.CanInteract(gameObject))
                {
                    continue;
                }

                if (candidates.Contains(interactable))
                {
                    continue;
                }

                Transform point = interactable.InteractionPoint;
                candidates.Add(interactable);
                candidatePositions.Add(point != null ? point.position : collider.transform.position);
            }

            int best = InteractionSelector.SelectBestIndex(
                candidatePositions, Origin, transform.forward, maxDistance, maxAngle, angleWeight);

            SetCurrent(best >= 0 ? candidates[best] : null);
        }

        private void SetCurrent(IInteractable next)
        {
            if (ReferenceEquals(Current, next))
            {
                return;
            }

            Current = next;
            CurrentChanged?.Invoke(next);
        }

        private void OnInteractPressed()
        {
            if (Current == null)
            {
                return;
            }

            if (!Current.CanInteract(gameObject))
            {
                SetCurrent(null);
                return;
            }

            GameLog.Info(LogChannel.Interaction, $"Interacting with '{Current.InteractionPrompt}'.", this);
            Current.Interact(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(Origin, maxDistance);
        }
#endif
    }
}
