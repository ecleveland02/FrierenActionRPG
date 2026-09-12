using Frieren.Characters;
using UnityEngine;

namespace Frieren.Magic
{
    /// <summary>
    /// Harms characters at the point of impact.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="MagicPulseEffect"/> on purpose. Damage acts on characters and pulses
    /// act on the world, and keeping them apart is what allows a spell that burns crates without
    /// hurting anyone, or a bolt that hurts without setting anything alight. A fire spell simply
    /// carries both.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Magic/Effect/Deal Damage", fileName = "Effect_Damage_", order = 1)]
    public sealed class DealDamageEffect : SpellEffect
    {
        private const int MaxTargets = 32;

        [SerializeField]
        [Min(0f)]
        private float amount = 20f;

        [SerializeField] private DamageType damageType = DamageType.Arcane;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Radius around the impact point. Zero damages only what was directly hit.")]
        private float radius;

        [SerializeField] private LayerMask affects = ~0;

        [SerializeField]
        [Tooltip("Whether the caster can be caught in its own area damage.")]
        private bool canHarmCaster;

        private static readonly Collider[] Overlap = new Collider[MaxTargets];

        public float Amount => amount;

        public DamageType DamageType => damageType;

        public override bool Apply(in SpellContext context)
        {
            var damage = new DamageInfo(amount, damageType, context.Caster);

            if (radius <= 0f)
            {
                return TryDamage(context.Target, damage, context.Caster);
            }

            int found = Physics.OverlapSphereNonAlloc(context.Point, radius, Overlap, affects,
                QueryTriggerInteraction.Ignore);
            bool anyHarmed = false;

            for (int i = 0; i < found; i++)
            {
                if (Overlap[i] != null && TryDamage(Overlap[i].gameObject, damage, context.Caster))
                {
                    anyHarmed = true;
                }
            }

            return anyHarmed;
        }

        private bool TryDamage(GameObject candidate, in DamageInfo damage, GameObject caster)
        {
            if (candidate == null)
            {
                return false;
            }

            var health = candidate.GetComponentInParent<CharacterHealth>();

            if (health == null)
            {
                return false;
            }

            if (!canHarmCaster && caster != null && health.gameObject == caster)
            {
                return false;
            }

            return health.TakeDamage(damage) > 0f;
        }
    }
}
