using Frieren.Core.Interaction;
using UnityEngine;

namespace Frieren.Core.Debugging
{
    /// <summary>
    /// A gray-box interactable that reports being used by changing colour and logging.
    /// </summary>
    /// <remarks>
    /// Exists so interaction can be verified before there is anything real to interact with. It is
    /// also the reference implementation of <see cref="IInteractable"/>: note that it owns its own
    /// state and its own feedback, and the player knows nothing about either.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DebugInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Use";

        [SerializeField]
        [Tooltip("Point the prompt anchors to and distance is measured from. Defaults to this transform.")]
        private Transform interactionPoint;

        [SerializeField] private Renderer tintTarget;

        [SerializeField] private Color idleColour = new Color(0.6f, 0.6f, 0.62f);

        [SerializeField] private Color usedColour = new Color(0.45f, 0.85f, 0.55f);

        [SerializeField]
        [Tooltip("Whether it can be used more than once.")]
        private bool repeatable = true;

        private MaterialPropertyBlock propertyBlock;
        private int baseColorId;
        private int colorId;

        public int UseCount { get; private set; }

        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;

        public string InteractionPrompt => prompt;

        private void Awake()
        {
            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            baseColorId = Shader.PropertyToID("_BaseColor");
            colorId = Shader.PropertyToID("_Color");
            ApplyTint(idleColour);
        }

        public bool CanInteract(GameObject actor) => repeatable || UseCount == 0;

        public void Interact(GameObject actor)
        {
            UseCount++;
            ApplyTint(usedColour);
            GameLog.Info(LogChannel.Interaction, $"{name} used by {actor.name} (use #{UseCount}).", this);
        }

        private void ApplyTint(Color colour)
        {
            if (tintTarget == null)
            {
                return;
            }

            tintTarget.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(baseColorId, colour);
            propertyBlock.SetColor(colorId, colour);
            tintTarget.SetPropertyBlock(propertyBlock);
        }
    }
}
