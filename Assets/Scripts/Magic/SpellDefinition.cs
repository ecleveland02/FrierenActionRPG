using System.Collections.Generic;
using Frieren.Characters.Animation;
using Frieren.Data;
using UnityEngine;

namespace Frieren.Magic
{
    /// <summary>
    /// An authored spell: its cost, its timing, how it aims, and what it does.
    /// </summary>
    /// <remarks>
    /// Nothing here is a spell-specific branch. Adding a spell means creating an asset and picking
    /// effects, not editing a switch statement, which is the difference between a magic system that
    /// grows and one that calcifies at about six spells.
    ///
    /// <see cref="Id"/> is what a save file records as a known spell, so it is a content contract:
    /// changing it after saves exist makes the player forget the spell.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Magic/Spell", fileName = "Spell_", order = 0)]
    public sealed class SpellDefinition : IdentifiableScriptableObject
    {
        [Header("Mode")]
        [SerializeField]
        [Tooltip("Instant resolves once. Channelled keeps working while the cast input is held.")]
        private SpellCastMode castMode = SpellCastMode.Instant;

        [Header("Cost and timing")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Paid once when the cast begins. For a channelled spell this is the cost to start it.")]
        private float manaCost = 10f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds between starting the cast and the effects firing. Zero resolves immediately.")]
        private float castTime = 0.4f;

        [SerializeField]
        [Min(0f)]
        private float cooldown = 0.5f;

        [Header("Channelling")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Mana drained per second while channelling. Ignored by instant spells.")]
        private float manaPerSecond = 8f;

        [SerializeField]
        [Min(0.02f)]
        [Tooltip("How often the effect list re-applies while channelling. Shorter is smoother and costs more.")]
        private float channelTickInterval = 0.1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Longest a single channel may run. Zero means until released or out of mana.")]
        private float maxChannelSeconds;

        [SerializeField]
        [Tooltip("Whether casting takes over the body. Turn off for spells you should be able to move during.")]
        private bool holdsActionLock = true;

        [Header("Targeting")]
        [SerializeField] private SpellTargeting targeting = SpellTargeting.Ray;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum distance for a Ray spell. Ignored by the other targeting modes.")]
        private float range = 25f;

        [SerializeField]
        [Tooltip("What a Ray spell can hit. Leave as Everything until layers are worth narrowing.")]
        private LayerMask aimMask = ~0;

        [Header("Behaviour")]
        [SerializeField]
        [Tooltip("Applied in order. This list is the spell; there is no code behind it.")]
        private List<SpellEffect> effects = new List<SpellEffect>();

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Animation played when the cast completes.")]
        private CharacterAction castAnimation = CharacterAction.CastRelease;

        public SpellCastMode CastMode => castMode;

        public bool IsChannelled => castMode == SpellCastMode.Channelled;

        public float ManaCost => manaCost;

        public float ManaPerSecond => manaPerSecond;

        public float ChannelTickInterval => Mathf.Max(0.02f, channelTickInterval);

        public float MaxChannelSeconds => maxChannelSeconds;

        /// <summary>
        /// Whether the cast claims <c>CharacterActionLock</c>. Off means locomotion keeps running,
        /// which is what a spell you are supposed to move around during needs.
        /// </summary>
        public bool HoldsActionLock => holdsActionLock;

        public float CastTime => castTime;

        public float Cooldown => cooldown;

        public SpellTargeting Targeting => targeting;

        public float Range => range;

        public LayerMask AimMask => aimMask;

        public IReadOnlyList<SpellEffect> Effects => effects;

        public CharacterAction CastAnimation => castAnimation;

        public bool HasEffects
        {
            get
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (effects[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public override string ToString() => $"{DisplayName} ({Id})";
    }
}
