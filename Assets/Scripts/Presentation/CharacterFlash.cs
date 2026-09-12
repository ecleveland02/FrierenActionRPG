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
    ///
    /// A skinned character takes the other path, and has to. The shell is a static mesh scaled by
    /// its transform, and a <c>SkinnedMeshRenderer</c> takes its vertex positions from bones and
    /// ignores its own scale, so a duplicated skinned shell would sit exactly inside the character
    /// and never be seen. Those characters are tinted directly instead. The objection that ruled
    /// that out above - that the placeholder animation rewrote the same colour every frame - does
    /// not apply to them: a rigged character is driven by an Animator, which does not touch
    /// renderer colour at all.
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

        [SerializeField]
        [Tooltip("Skinned renderers to tint when there is no mesh to build a shell from. " +
                 "Defaults to every one found in children.")]
        private SkinnedMeshRenderer[] skinned;

        private MeshRenderer shell;
        private bool tintingDirectly;
        private bool tintApplied;
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

            if (source != null)
            {
                BuildShell();
                return;
            }

            if (skinned == null || skinned.Length == 0)
            {
                skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            tintingDirectly = skinned != null && skinned.Length > 0;

            if (!tintingDirectly)
            {
                Debug.LogWarning(
                    $"{name}: CharacterFlash found neither a mesh to shell nor a skinned renderer " +
                    "to tint, so hits will not read on this character.", this);
            }
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
            if (tintingDirectly)
            {
                TintSkinned();
                return;
            }

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

        /// <summary>
        /// Colours a rigged character's own renderers.
        /// </summary>
        /// <remarks>
        /// The shell conveys a flash by collapsing, because it is opaque and cannot fade. This one
        /// can: the tint blends from the character's own colour to the flash colour and back, so
        /// the same event reads without needing a second copy of a skinned mesh.
        ///
        /// Clearing sets a null property block rather than writing white, which restores whatever
        /// the material actually says instead of assuming it was untinted to begin with.
        /// </remarks>
        private void TintSkinned()
        {
            bool flashing = IsFlashing;

            if (!flashing && !hasSustained)
            {
                if (tintApplied)
                {
                    for (int i = 0; i < skinned.Length; i++)
                    {
                        if (skinned[i] != null)
                        {
                            skinned[i].SetPropertyBlock(null);
                        }
                    }

                    tintApplied = false;
                }

                return;
            }

            Color colour = sustainedColour;

            if (flashing)
            {
                float remaining = flashDuration <= 0f
                    ? 0f
                    : Mathf.Clamp01((flashEndsAt - Time.time) / flashDuration);

                // Fade the flash out over its life, falling back to the sustained colour underneath
                // it rather than to nothing, so a hit during a wind-up leaves the wind-up showing.
                colour = hasSustained
                    ? Color.Lerp(sustainedColour, flashColour, remaining)
                    : Color.Lerp(Color.white, flashColour, remaining);
            }

            for (int i = 0; i < skinned.Length; i++)
            {
                if (skinned[i] == null)
                {
                    continue;
                }

                skinned[i].GetPropertyBlock(properties);
                properties.SetColor(baseColorId, colour);
                properties.SetColor(colorId, colour);
                skinned[i].SetPropertyBlock(properties);
            }

            tintApplied = true;
        }
    }
}
