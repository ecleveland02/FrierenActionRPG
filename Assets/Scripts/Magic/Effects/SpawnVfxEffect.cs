using UnityEngine;

namespace Frieren.Magic.Effects
{
    /// <summary>
    /// Spawns a visual effect where a spell landed. Purely cosmetic: it changes nothing in the world.
    /// </summary>
    /// <remarks>
    /// An effect rather than something wired into the caster, so a spell's look is authored in the
    /// same list as everything else it does. Adding a flash to a spell is dragging one more asset
    /// into its effects, and one flash asset can be shared by every spell that should look alike.
    /// Nothing here knows which spell is casting it, which is what lets a hundred spells reuse a
    /// dozen effects.
    ///
    /// It always reports false from <see cref="Apply"/>. The return value means "this affected
    /// something", and the caster uses it to tell a hit from a miss; a spark that claimed to have
    /// affected the world would make every spell report a hit on empty air.
    ///
    /// Lifetime is a plain timed destroy rather than a pool. Pooling matters when spawn rates get
    /// high, and this is one instance per cast; a pool added later slots in behind this same field
    /// without touching a single spell asset.
    ///
    /// A channelled spell applies its whole effect list on every tick, which for a ward held down
    /// for two seconds is more than a dozen spawns. <c>oneInstancePerAnchor</c> is the answer, and
    /// it asks the scene rather than remembering anything: the instance is given a known name and
    /// the anchor is checked for a child already carrying it. That keeps this asset stateless, as
    /// <see cref="SpellEffect"/> requires, and it stays correct with two casters channelling at
    /// once, which a static "last spawned at" field would not.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Magic/Effects/Spawn VFX", fileName = "Effect_Vfx_", order = 40)]
    public sealed class SpawnVfxEffect : SpellEffect
    {
        /// <summary>Where the effect is placed.</summary>
        public enum Placement
        {
            /// <summary>Where the spell landed. The default, and what an impact wants.</summary>
            ImpactPoint = 0,

            /// <summary>On the caster. For wards, buffs and anything that reads as coming from them.</summary>
            Caster = 1,

            /// <summary>On whatever was hit, following it if it moves.</summary>
            Target = 2,
        }

        /// <summary>How the effect is turned to face.</summary>
        public enum Facing
        {
            /// <summary>Aligned to the surface it hit. Right for a scorch or a splash.</summary>
            SurfaceNormal = 0,

            /// <summary>Along the spell's travel. Right for a beam or a bolt.</summary>
            SpellDirection = 1,

            /// <summary>Upright, ignoring both. Right for a magic circle on the ground.</summary>
            WorldUp = 2,
        }

        [SerializeField]
        [Tooltip("The particle prefab to spawn. Nothing happens if this is empty.")]
        private GameObject prefab;

        [SerializeField] private Placement placement = Placement.ImpactPoint;

        [SerializeField] private Facing facing = Facing.SurfaceNormal;

        [SerializeField]
        [Tooltip("Offset from the spawn point, in the effect's own space after facing is applied.")]
        private Vector3 offset = Vector3.zero;

        [SerializeField]
        [Min(0.01f)]
        private float scale = 1f;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Seconds before the instance is destroyed. Give it longer than the particles last.")]
        private float lifetime = 3f;

        [SerializeField]
        [Tooltip("Parent to the target so the effect follows it. Only meaningful for Caster and Target.")]
        private bool attachToTarget = true;

        [SerializeField]
        [Tooltip("Allow only one of these on a given anchor at a time. Required for any channelled " +
                 "spell, which applies its effects on every tick.")]
        private bool oneInstancePerAnchor;

        public override bool Apply(in SpellContext context)
        {
            if (prefab == null)
            {
                return false;
            }

            Transform anchor = ResolveAnchor(context);

            if (oneInstancePerAnchor && anchor != null && AlreadyPresent(anchor))
            {
                return false;
            }

            Vector3 position = anchor != null ? anchor.position : context.Point;
            Quaternion rotation = ResolveRotation(context);

            GameObject instance = Object.Instantiate(prefab, position + rotation * offset, rotation);
            instance.transform.localScale = prefab.transform.localScale * scale;
            instance.name = InstanceName;

            if (attachToTarget && anchor != null)
            {
                // worldPositionStays, so the offset computed above is not undone by the parent's
                // own transform.
                instance.transform.SetParent(anchor, true);
            }

            Object.Destroy(instance, lifetime);

            // Cosmetic only. Saying otherwise would make every spell report a hit on thin air.
            return false;
        }

        /// <summary>
        /// The name every instance of this effect carries, so one can be recognised later.
        /// </summary>
        /// <remarks>
        /// Keyed on the asset, not the prefab: two effects sharing a prefab at different scales are
        /// different effects and should not suppress one another.
        /// </remarks>
        private string InstanceName => "VFX_" + name;

        private bool AlreadyPresent(Transform anchor)
        {
            for (int i = 0; i < anchor.childCount; i++)
            {
                if (anchor.GetChild(i).name == InstanceName)
                {
                    return true;
                }
            }

            return false;
        }

        private Transform ResolveAnchor(in SpellContext context)
        {
            switch (placement)
            {
                case Placement.Caster:
                    return context.Caster != null ? context.Caster.transform : null;

                case Placement.Target:
                    return context.Target != null ? context.Target.transform : null;

                default:
                    return null;
            }
        }

        private Quaternion ResolveRotation(in SpellContext context)
        {
            switch (facing)
            {
                case Facing.SpellDirection:
                    return context.Direction.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(context.Direction, Vector3.up)
                        : Quaternion.identity;

                case Facing.WorldUp:
                    return Quaternion.identity;

                default:
                    // A normal pointing straight up gives LookRotation no usable up vector, which is
                    // the common case for a spell that hit the floor.
                    return context.Normal.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(context.Normal, Vector3.up) * Quaternion.Euler(90f, 0f, 0f)
                        : Quaternion.identity;
            }
        }
    }
}
