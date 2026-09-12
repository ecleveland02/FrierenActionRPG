using System.Collections.Generic;
using Frieren.Characters;
using Frieren.Core.Magic;
using Frieren.Magic;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>
    /// Draws the line a spell resolved along, coloured by what the spell was made of.
    /// </summary>
    /// <remarks>
    /// Targeting is instant, so without this a Zoltraak is a log line and a number. The beam is
    /// drawn along the already-resolved line rather than travelling, which is exactly what the
    /// targeting design said a projectile would be: presentation played over a result that was
    /// decided the moment the button went down.
    ///
    /// The colour comes from the spell's own effects - it asks the first pulse effect what element
    /// it emits, and falls back to the damage type - so a new spell is coloured correctly the day
    /// it is authored, with nothing to configure. A spell that carries Heat is orange because it
    /// carries Heat, not because someone remembered to set a swatch.
    /// </remarks>
    [RequireComponent(typeof(CharacterSpellcaster))]
    [DisallowMultipleComponent]
    public sealed class SpellTracer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Seconds the beam stays visible after a cast.")]
        private float lifetime = 0.22f;

        [SerializeField] private float startWidth = 0.16f;

        [SerializeField] private float endWidth = 0.05f;

        [SerializeField]
        [Tooltip("Drop the beam's start this far below the aim source, so it leaves the hands rather than the eyes.")]
        private float originDrop = 0.35f;

        private static readonly Dictionary<MagicElement, Color> ElementColours = new Dictionary<MagicElement, Color>
        {
            { MagicElement.Arcane, new Color(0.75f, 0.55f, 1f) },
            { MagicElement.Heat, new Color(1f, 0.5f, 0.15f) },
            { MagicElement.Cold, new Color(0.6f, 0.9f, 1f) },
            { MagicElement.Force, new Color(0.8f, 0.8f, 0.85f) },
            { MagicElement.Water, new Color(0.3f, 0.6f, 1f) },
            { MagicElement.Restoration, new Color(0.5f, 1f, 0.6f) },
            { MagicElement.Unbinding, new Color(1f, 0.9f, 0.4f) },
            { MagicElement.Warding, new Color(0.5f, 0.8f, 1f) },
        };

        private CharacterSpellcaster spellcaster;
        private LineRenderer line;
        private float hideAt;

        private void Awake()
        {
            spellcaster = GetComponent<CharacterSpellcaster>();
            BuildLine();
        }

        private void BuildLine()
        {
            var host = new GameObject("SpellBeam");
            host.transform.SetParent(transform, false);

            line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            // Sprites/Default is vertex-coloured and always included in a build, so the beam takes
            // its colour without a material asset and without hunting for a shader that may not ship.
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                line.material = new Material(shader);
            }
        }

        private void OnEnable() => spellcaster.CastResolved += OnCastResolved;

        private void OnDisable() => spellcaster.CastResolved -= OnCastResolved;

        private void OnCastResolved(SpellDefinition spell, SpellContext context, bool affected)
        {
            if (line == null || spell == null || spell.Targeting == SpellTargeting.Self)
            {
                return;
            }

            Color colour = ColourFor(spell);

            // A cast that hit nothing is dimmer, so a miss is distinguishable from a hit on
            // something that does not care - the same distinction the console already reports.
            if (!affected)
            {
                colour *= 0.45f;
                colour.a = 1f;
            }

            line.startColor = colour;
            line.endColor = new Color(colour.r, colour.g, colour.b, 0.15f);
            line.SetPosition(0, context.Origin - Vector3.up * originDrop);
            line.SetPosition(1, context.Point);
            line.enabled = true;
            hideAt = Time.time + lifetime;
        }

        private void Update()
        {
            if (line != null && line.enabled && Time.time >= hideAt)
            {
                line.enabled = false;
            }
        }

        /// <summary>
        /// Asks the spell what it is made of. First pulse element wins; failing that, the damage
        /// type; failing that, plain arcane.
        /// </summary>
        private static Color ColourFor(SpellDefinition spell)
        {
            for (int i = 0; i < spell.Effects.Count; i++)
            {
                if (spell.Effects[i] is MagicPulseEffect pulse &&
                    ElementColours.TryGetValue(pulse.Element, out Color byElement))
                {
                    return byElement;
                }
            }

            for (int i = 0; i < spell.Effects.Count; i++)
            {
                switch (spell.Effects[i])
                {
                    case PiercingDamageEffect piercing:
                        return ColourForDamage(piercing.DamageType);
                    case DealDamageEffect damage:
                        return ColourForDamage(damage.DamageType);
                }
            }

            return ElementColours[MagicElement.Arcane];
        }

        private static Color ColourForDamage(DamageType type) => type switch
        {
            DamageType.Fire => ElementColours[MagicElement.Heat],
            DamageType.Ice => ElementColours[MagicElement.Cold],
            DamageType.Physical => ElementColours[MagicElement.Force],
            _ => ElementColours[MagicElement.Arcane],
        };
    }
}
