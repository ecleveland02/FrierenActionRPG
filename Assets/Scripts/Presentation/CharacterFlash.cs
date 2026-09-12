using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>
    /// Colours a character briefly, or holds a colour on it, without touching its own renderer.
    /// </summary>
    /// <remarks>
    /// The obvious implementation - tint the character's material - cannot be used here.
    /// <c>PlaceholderCharacterAnimation</c> already owns that renderer's colour and rewrites it
    /// every frame from speed and grounding, so a flash written to the same property block would
    /// last exactly one frame and then be argued away.
    ///
    /// So this builds a shell instead: a copy of the character's mesh, scaled up a few percent,
    /// disabled until something wants it. Nothing contends for it, it survives whatever the
    /// animation layer is doing, and it reads as an aura rather than as the character changing
    /// colour, which is what a hit reaction should look like anyway.
    ///
    /// Two channels, because they mean different things. A <see cref="Flash"/> is an event - it was
    /// hit - and expires on its own. <see cref="SetSustained"/> is a state - it is winding up a
    /// swing - and lasts until cleared. A flash wins while it is running, so being hit mid-wind-up
    /// still reads.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterFlash : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Mesh to copy. Defaults to the first one found in children.")]
        private MeshFilter source;

        [SerializeField]
        [Min(1f)]
        [Tooltip("How much larger than the character the shell is. Just enough to read as an outline.")]
        private float scale = 1.08f;

        private const float FlashPunch = 0.22f;

        private MeshRenderer shell;
        private MaterialPropertyBlock properties;
        private int baseColorId;
        private int colorId;

        private float flashEndsAt;
        private float flashDuration;
        private Color flashColour;
        private bool hasSustained;
        private Color sustainedColour;

        public bool IsFlashing => Time.time < flashEndsAt;

        private void Awake()
        {
            baseColorId = Shader.PropertyToID("_BaseColor");
            colorId = Shader.PropertyToID("_Color");
            properties = new MaterialPropertyBlock();

            if (source == null)
            {
                source = GetComponentInChildren<MeshFilter>();
            }

            BuildShell();
        }

        private void BuildShell()
        {
            if (source == null)
            {
                return;
            }

            var host = new GameObject("FlashShell");
            host.transform.SetParent(source.transform, false);
            host.transform.localScale = Vector3.one * scale;
            host.layer = gameObject.layer;

            host.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;

            shell = host.AddComponent<MeshRenderer>();

            var reference = source.GetComponent<MeshRenderer>();

            if (reference != null)
            {
                shell.sharedMaterial = reference.sharedMaterial;
            }

            // A shell that casts shadows doubles every shadow in the scene, and one that receives
            // them picks up the character's own.
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.enabled = false;
        }

        /// <summary>Colours the character for a moment. Overrides any sustained colour while it runs.</summary>
        public void Flash(Color colour, float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            flashColour = colour;
            flashDuration = seconds;
            flashEndsAt = Time.time + seconds;
        }

        /// <summary>Holds a colour until cleared. For states, not events.</summary>
        public void SetSustained(Color colour)
        {
            hasSustained = true;
            sustainedColour = colour;
        }

        public void ClearSustained() => hasSustained = false;

        private void LateUpdate()
        {
            if (shell == null)
            {
                return;
            }

            bool flashing = IsFlashing;

            if (!flashing && !hasSustained)
            {
                shell.enabled = false;
                return;
            }

            float restingScale = hasSustained ? scale : 1f;
            float size = restingScale;
            Color colour = sustainedColour;

            if (flashing)
            {
                // The shell is opaque, so a flash cannot fade by alpha. It collapses instead:
                // it starts proud of the character and shrinks back to rest, which reads as an
                // impact ring and needs no transparent material to author.
                float remaining = flashDuration <= 0f
                    ? 0f
                    : Mathf.Clamp01((flashEndsAt - Time.time) / flashDuration);
                size = Mathf.Lerp(restingScale, scale + FlashPunch, remaining);
                colour = flashColour;
            }

            shell.transform.localScale = Vector3.one * size;
            shell.enabled = size > 1.001f;

            if (!shell.enabled)
            {
                return;
            }

            shell.GetPropertyBlock(properties);
            properties.SetColor(baseColorId, colour);
            properties.SetColor(colorId, colour);
            shell.SetPropertyBlock(properties);
        }

    }
}
