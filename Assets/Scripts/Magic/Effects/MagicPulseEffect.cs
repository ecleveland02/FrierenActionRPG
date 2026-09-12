using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.Magic
{
    /// <summary>
    /// Delivers magical influence of one element to everything nearby that can receive it.
    /// </summary>
    /// <remarks>
    /// The effect that makes the world reactive. It knows nothing about crates, locks or water - it
    /// broadcasts an element and a magnitude, and objects decide for themselves whether they care.
    /// A new spell that emits Heat lights every flammable object in the game the day it is authored,
    /// with no change to any of them.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Magic/Effect/Magic Pulse", fileName = "Effect_Pulse_", order = 0)]
    public sealed class MagicPulseEffect : SpellEffect
    {
        private const int MaxReceivers = 32;

        [SerializeField] private MagicElement element = MagicElement.Arcane;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Strength delivered. Compared against each receiver's own thresholds.")]
        private float magnitude = 10f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Radius around the impact point. Zero affects only what was directly hit.")]
        private float radius = 1.5f;

        [SerializeField] private LayerMask affects = ~0;

        private static readonly Collider[] Overlap = new Collider[MaxReceivers];

        public MagicElement Element => element;

        public float Magnitude => magnitude;

        public override bool Apply(in SpellContext context)
        {
            var pulse = new MagicPulse(element, magnitude, context.Point, context.Direction, context.Caster);

            if (radius <= 0f)
            {
                return TryDeliver(context.Target, pulse);
            }

            int found = Physics.OverlapSphereNonAlloc(context.Point, radius, Overlap, affects,
                QueryTriggerInteraction.Collide);
            bool affected = false;

            for (int i = 0; i < found; i++)
            {
                if (Overlap[i] != null && TryDeliver(Overlap[i].gameObject, pulse))
                {
                    affected = true;
                }
            }

            return affected;
        }

        /// <summary>
        /// Looks up the hierarchy from the collider, so a receiver can put its colliders on children
        /// without every world object needing its collider on the same object as its script.
        /// </summary>
        private static bool TryDeliver(GameObject candidate, in MagicPulse pulse)
        {
            if (candidate == null)
            {
                return false;
            }

            var receiver = candidate.GetComponentInParent<IMagicReceiver>();
            return receiver != null && receiver.ReceiveMagic(pulse);
        }
    }
}
