using UnityEngine;

namespace Frieren.Characters.Animation
{
    /// <summary>
    /// Shows character state on an un-rigged primitive by squashing and tinting it.
    /// </summary>
    /// <remarks>
    /// Gray-boxing without this means every movement bug looks identical: a capsule sliding around.
    /// Being able to see at a glance that the character believes it is airborne, or that a dodge
    /// actually fired, is worth the sixty lines. It is deleted the moment a real rig exists.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlaceholderCharacterAnimation : MonoBehaviour, ICharacterAnimation
    {
        [SerializeField]
        [Tooltip("Visual child to deform. Deforming the root would fight the CharacterController's capsule.")]
        private Transform visual;

        [SerializeField] private Renderer tintTarget;

        [Header("Colours")]
        [SerializeField] private Color idleColour = new Color(0.75f, 0.78f, 0.85f);
        [SerializeField] private Color movingColour = new Color(0.45f, 0.68f, 0.9f);
        [SerializeField] private Color airborneColour = new Color(0.9f, 0.78f, 0.4f);
        [SerializeField] private Color actionColour = new Color(0.95f, 0.45f, 0.5f);

        [SerializeField]
        [Tooltip("How long the action flash lasts, in seconds.")]
        private float actionFlashDuration = 0.25f;

        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseScale = Vector3.one;
        private float actionFlashRemaining;
        private int baseColorId;
        private int colorId;

        private void Awake()
        {
            if (visual == null)
            {
                visual = transform;
            }

            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>();
            }

            baseScale = visual.localScale;
            propertyBlock = new MaterialPropertyBlock();

            // Built-in and URP use different property names; set whichever the shader has.
            baseColorId = Shader.PropertyToID("_BaseColor");
            colorId = Shader.PropertyToID("_Color");
        }

        private void Update()
        {
            if (actionFlashRemaining > 0f)
            {
                actionFlashRemaining -= Time.deltaTime;
            }
        }

        public void SetLocomotion(float planarSpeed, float normalizedSpeed, bool isGrounded, float verticalVelocity)
        {
            if (visual != null)
            {
                // Stretch upward while rising, squash while falling. Reads as intent, not realism.
                float stretch = Mathf.Clamp(verticalVelocity * 0.015f, -0.12f, 0.18f);
                float vertical = 1f + (isGrounded ? 0f : stretch);
                float horizontal = 1f - (isGrounded ? 0f : stretch * 0.5f);

                visual.localScale = new Vector3(
                    baseScale.x * horizontal,
                    baseScale.y * vertical,
                    baseScale.z * horizontal);
            }

            Color colour = actionFlashRemaining > 0f ? actionColour
                : !isGrounded ? airborneColour
                : normalizedSpeed > 0.05f ? Color.Lerp(idleColour, movingColour, Mathf.Clamp01(normalizedSpeed))
                : idleColour;

            ApplyTint(colour);
        }

        public void PlayAction(CharacterAction action)
        {
            actionFlashRemaining = actionFlashDuration;
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
