using UnityEngine;

namespace Frieren.Magic
{
    /// <summary>
    /// One thing a spell does. Spells are lists of these.
    /// </summary>
    /// <remarks>
    /// This is the design's centre of gravity. A spell is not a class; it is a definition asset
    /// holding an ordered list of effects, so "fire that also lights torches" is authored by adding
    /// an effect to a list rather than by writing a class. It is also what makes a new spell able to
    /// work on existing world objects for free, because it reuses effects those objects already
    /// understand.
    ///
    /// Effects are ScriptableObjects, so one asset can be shared by several spells and tuned once.
    /// They must therefore hold no per-cast state - everything they need arrives in the
    /// <see cref="SpellContext"/>.
    /// </remarks>
    public abstract class SpellEffect : ScriptableObject
    {
        [SerializeField]
        [TextArea(1, 3)]
        [Tooltip("What this effect does, for whoever opens the spell asset later.")]
        private string editorNote;

        public string EditorNote => editorNote;

        /// <summary>
        /// Applies the effect.
        /// </summary>
        /// <returns>
        /// True if it affected anything. A spell reports this so feedback can distinguish a miss
        /// from a hit on something that does not care.
        /// </returns>
        public abstract bool Apply(in SpellContext context);
    }
}
