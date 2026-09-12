using System;
using System.Collections.Generic;
using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Magic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Player
{
    /// <summary>
    /// Turns the player's cast input into a call on <see cref="CharacterSpellcaster"/>.
    /// </summary>
    /// <remarks>
    /// Input stays in the player layer, exactly as it does for locomotion. The spellcaster reads no
    /// input at all, which is what lets an enemy in Milestone 5 cast the same spells through the
    /// same component with a behaviour tree driving it instead of a mouse.
    ///
    /// Spell selection is a numeric row for now. Real spell discovery, a hotbar and a spell menu are
    /// later milestones; this exists so three spells can be tested without any UI.
    /// </remarks>
    [RequireComponent(typeof(CharacterSpellcaster))]
    [DisallowMultipleComponent]
    public sealed class PlayerSpellInput : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;

        [SerializeField]
        [Tooltip("Spells the player can currently cast. Selected with the number row.")]
        private List<SpellDefinition> knownSpells = new List<SpellDefinition>();

        private CharacterSpellcaster spellcaster;

        /// <summary>Raised when the selected spell changes, for a HUD that does not exist yet.</summary>
        public event Action<SpellDefinition> SelectionChanged;

        public int SelectedIndex { get; private set; }

        public SpellDefinition SelectedSpell =>
            SelectedIndex >= 0 && SelectedIndex < knownSpells.Count ? knownSpells[SelectedIndex] : null;

        public IReadOnlyList<SpellDefinition> KnownSpells => knownSpells;

        private void Awake()
        {
            spellcaster = GetComponent<CharacterSpellcaster>();

            if (inputReader == null)
            {
                GameLog.Error(LogChannel.Magic,
                    $"{name}: PlayerSpellInput has no InputReader, so casting will never fire.", this);
            }

            if (knownSpells.Count == 0)
            {
                GameLog.Warn(LogChannel.Magic, $"{name}: PlayerSpellInput knows no spells.", this);
            }
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.CastPerformed += OnCastPressed;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.CastPerformed -= OnCastPressed;
            }
        }

        private void Update() => ReadSelectionKeys();

        /// <summary>
        /// Selection is read directly from the keyboard rather than through the input asset. A spell
        /// hotbar is a UI feature with its own bindings, and adding eight throwaway actions to the
        /// gameplay map now would mean removing them later.
        /// </summary>
        private void ReadSelectionKeys()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || knownSpells.Count == 0)
            {
                return;
            }

            for (int i = 0; i < knownSpells.Count && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Select(i);
                    return;
                }
            }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= knownSpells.Count || index == SelectedIndex)
            {
                return;
            }

            SelectedIndex = index;
            GameLog.Info(LogChannel.Magic, $"Selected spell {SelectedSpell}.", this);
            SelectionChanged?.Invoke(SelectedSpell);
        }

        private void OnCastPressed()
        {
            if (SelectedSpell == null)
            {
                GameLog.Warn(LogChannel.Magic, "Cast pressed with no spell selected.", this);
                return;
            }

            spellcaster.TryCast(SelectedSpell);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            var area = new Rect(10f, 337f, 420f, 22f + knownSpells.Count * 18f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Spells - number keys select, Cast (LMB / RT) casts");

            for (int i = 0; i < knownSpells.Count; i++)
            {
                SpellDefinition spell = knownSpells[i];

                if (spell == null)
                {
                    continue;
                }

                float cooldown = spellcaster != null ? spellcaster.CooldownRemaining(spell) : 0f;
                string marker = i == SelectedIndex ? ">" : " ";
                string state = cooldown > 0f ? $"  cooling {cooldown:0.0}s" : string.Empty;
                GUILayout.Label($"{marker} {i + 1}. {spell.DisplayName}   {spell.ManaCost:0} mana{state}");
            }

            GUILayout.EndArea();
        }
#endif
    }
}
