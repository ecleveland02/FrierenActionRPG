using Frieren.Core.Interaction;
using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// Something you deliberately act on to finish an objective: a lectern, a seal, a lever.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="ObjectiveVolume"/>. A volume marks arriving somewhere, which
    /// should not care how you arrived; this marks doing something on purpose, which should. The
    /// end of a slice wants the second - reaching the top is not the same as finishing.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ObjectiveInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private SliceObjectives objectives;

        [SerializeField] private string goalId;

        [SerializeField] private Transform interactionPoint;

        [SerializeField] private string prompt = "Read";

        [SerializeField] private string usedPrompt = "Read again";

        [SerializeField]
        [Tooltip("Tinted once used, so a finished objective looks finished.")]
        private Renderer tintTarget;

        [SerializeField] private Color usedColour = new Color(0.55f, 0.95f, 0.65f);

        private MaterialPropertyBlock properties;
        private bool used;

        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;

        public string InteractionPrompt => used ? usedPrompt : prompt;

        public bool CanInteract(GameObject actor) => true;

        public void Interact(GameObject actor)
        {
            if (objectives != null)
            {
                objectives.Complete(goalId);
            }

            if (used)
            {
                return;
            }

            used = true;
            Tint();
        }

        private void Tint()
        {
            if (tintTarget == null)
            {
                return;
            }

            properties ??= new MaterialPropertyBlock();
            tintTarget.GetPropertyBlock(properties);
            properties.SetColor(Shader.PropertyToID("_BaseColor"), usedColour);
            properties.SetColor(Shader.PropertyToID("_Color"), usedColour);
            tintTarget.SetPropertyBlock(properties);
        }
    }
}
