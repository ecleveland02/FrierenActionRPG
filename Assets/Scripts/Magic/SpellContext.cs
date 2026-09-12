using UnityEngine;

namespace Frieren.Magic
{
    /// <summary>
    /// Everything an effect needs to know about the cast it belongs to.
    /// </summary>
    /// <remarks>
    /// Passed to every effect in a spell's list, so effects stay ignorant of each other and of how
    /// targeting was resolved. An effect that dealt damage would work identically whether the spell
    /// was a ray, a self-cast or an area, which is what lets effects be recombined freely.
    ///
    /// A readonly struct passed by <c>in</c>: effects must not be able to rewrite the cast out from
    /// under the effects that run after them.
    /// </remarks>
    public readonly struct SpellContext
    {
        public SpellContext(GameObject caster, SpellDefinition spell, Vector3 origin, Vector3 direction,
            Vector3 point, GameObject target = null, Vector3 normal = default)
        {
            Caster = caster;
            Spell = spell;
            Origin = origin;
            Direction = direction;
            Point = point;
            Target = target;
            Normal = normal;
        }

        public GameObject Caster { get; }

        public SpellDefinition Spell { get; }

        /// <summary>Where the cast started, roughly the caster's hands.</summary>
        public Vector3 Origin { get; }

        /// <summary>Normalised aim direction.</summary>
        public Vector3 Direction { get; }

        /// <summary>Where the spell resolved: what the ray hit, or the point at maximum range.</summary>
        public Vector3 Point { get; }

        /// <summary>What was hit, if anything. Null for a miss or an untargeted cast.</summary>
        public GameObject Target { get; }

        /// <summary>Surface normal at <see cref="Point"/>, for effects that orient to a surface.</summary>
        public Vector3 Normal { get; }
    }
}
