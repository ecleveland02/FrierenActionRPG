using System;
using Frieren.Characters;
using Frieren.Core;
using UnityEngine;

namespace Frieren.Magic
{
    /// <summary>
    /// Harms every character along the aim line rather than only the first one hit.
    /// </summary>
    /// <remarks>
    /// Zoltraak's defining property is that it does not stop at the first body. Expressing that as
    /// an effect rather than as a spell class keeps the pattern intact: any spell can be made
    /// piercing by swapping which damage effect is in its list, and a piercing spell that also
    /// scorches the wall behind still just adds a pulse effect beside this one.
    ///
    /// The line is re-cast here rather than reusing <see cref="SpellContext.Target"/>, because the
    /// caster's targeting stopped at the first hit by definition. <see cref="SpellContext.Point"/>
    /// is therefore ignored; this effect works from origin and direction.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Magic/Effect/Piercing Damage", fileName = "Effect_Pierce_", order = 2)]
    public sealed class PiercingDamageEffect : SpellEffect
    {
        private const int MaxHits = 32;

        [SerializeField]
        [Min(0f)]
        private float amount = 45f;

        [SerializeField] private DamageType damageType = DamageType.Arcane;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Overrides the spell's range when above zero. Leave at zero to use the spell's own range.")]
        private float rangeOverride;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Damage retained by each successive target. 1 pierces without loss.")]
        private float falloffPerTarget = 0.8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Radius of the beam. Zero is a hairline ray, which is unforgiving to aim.")]
        private float thickness = 0.35f;

        [SerializeField]
        [Tooltip("Who the beam can harm.")]
        private LayerMask affects = GameLayers.CombatantsMask;

        [SerializeField]
        [Tooltip("Solid geometry that ends the beam. Piercing bodies is the point; piercing walls is not.")]
        private LayerMask stoppedBy = GameLayers.SolidMask;

        private static readonly RaycastHit[] Hits = new RaycastHit[MaxHits];

        public float Amount => amount;

        public DamageType DamageType => damageType;

        public override bool Apply(in SpellContext context)
        {
            Vector3 direction = context.Direction.sqrMagnitude > 0.0001f
                ? context.Direction.normalized
                : Vector3.forward;

            float range = rangeOverride > 0f ? rangeOverride
                : context.Spell != null ? context.Spell.Range
                : 30f;

            // A thin ray finds the wall, and the beam is clipped to it. Testing occlusion with the
            // thick cast instead would let a shot that merely grazes the floor stop dead.
            if (stoppedBy != 0 && Physics.Raycast(context.Origin, direction, out RaycastHit blocker,
                    range, stoppedBy, QueryTriggerInteraction.Ignore))
            {
                range = blocker.distance;
            }

            if (range <= 0.01f)
            {
                return false;
            }

            int found = thickness > 0f
                ? Physics.SphereCastNonAlloc(context.Origin, thickness, direction, Hits, range, affects,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(context.Origin, direction, Hits, range, affects,
                    QueryTriggerInteraction.Ignore);

            if (found <= 0)
            {
                return false;
            }

            // SphereCastNonAlloc does not guarantee order, and falloff is meaningless unless targets
            // are resolved nearest first.
            Array.Sort(Hits, 0, found, HitDistanceComparer.Instance);

            float current = amount;
            bool anyHarmed = false;
            CharacterHealth lastHealth = null;

            for (int i = 0; i < found; i++)
            {
                var health = Hits[i].collider != null
                    ? Hits[i].collider.GetComponentInParent<CharacterHealth>()
                    : null;

                if (health == null || health.gameObject == context.Caster)
                {
                    continue;
                }

                // One character can present several colliders to a single cast.
                if (ReferenceEquals(health, lastHealth))
                {
                    continue;
                }

                lastHealth = health;

                if (health.TakeDamage(new DamageInfo(current, damageType, context.Caster)) > 0f)
                {
                    anyHarmed = true;
                }

                current *= falloffPerTarget;

                if (current <= 0.5f)
                {
                    break;
                }
            }

            return anyHarmed;
        }

        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
