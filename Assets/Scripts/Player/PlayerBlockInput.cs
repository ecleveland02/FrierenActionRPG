using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Magic;
using UnityEngine;

namespace Frieren.Player
{
    /// <summary>
    /// Holds a ward up while the block button is held.
    /// </summary>
    /// <remarks>
    /// Blocking is a spell, not a special case. It runs the same <see cref="SpellDefinition"/>
    /// through the same <see cref="CharacterSpellcaster"/> as everything else, so its cost, its
    /// drain and what it actually does stay authored in an asset rather than hard-coded here. All
    /// this component changes is which button starts it.
    ///
    /// Taking it off the spell wheel is a real design decision, not just a rebind. Defence you have
    /// to select is defence you will not use, and cycling to it mid-swing is the opposite of what a
    /// block is for. It gets its own button and stops competing with offence for a slot.
    ///
    /// Blocking occupies the caster: the spellcaster runs one spell at a time, so the cast button
    /// is refused while a ward is up and says why. That reads as concentration and suits the
    /// setting. If it turns out to feel bad, the fix is a second caster or a flag on the spell -
    /// not a special path for this one button.
    /// </remarks>
    [RequireComponent(typeof(CharacterSpellcaster))]
    [DisallowMultipleComponent]
    public sealed class PlayerBlockInput : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;

        [SerializeField]
        [Tooltip("The spell held while blocking. A channelled, self-targeted ward.")]
        private SpellDefinition blockSpell;

        private CharacterSpellcaster spellcaster;

        public SpellDefinition BlockSpell => blockSpell;

        /// <summary>True while this component's own spell is the one being channelled.</summary>
        public bool IsBlocking =>
            spellcaster != null && blockSpell != null &&
            spellcaster.IsChannelling && spellcaster.CurrentSpell == blockSpell;

        private void Awake()
        {
            spellcaster = GetComponent<CharacterSpellcaster>();

            if (inputReader == null)
            {
                GameLog.Error(LogChannel.Magic,
                    $"{name}: PlayerBlockInput has no InputReader, so blocking will never fire.", this);
            }

            if (blockSpell == null)
            {
                GameLog.Error(LogChannel.Magic,
                    $"{name}: PlayerBlockInput has no block spell assigned, so the block button does nothing.", this);
            }
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.BlockPerformed += OnBlockPressed;
                inputReader.BlockReleased += OnBlockReleased;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.BlockPerformed -= OnBlockPressed;
                inputReader.BlockReleased -= OnBlockReleased;
            }

            // Scoped, so switching this component off cannot drop someone else's channel.
            spellcaster.ReleaseChannel(blockSpell);
        }

        private void OnBlockPressed()
        {
            if (blockSpell != null)
            {
                spellcaster.TryCast(blockSpell);
            }
        }

        private void OnBlockReleased() => spellcaster.ReleaseChannel(blockSpell);
    }
}
