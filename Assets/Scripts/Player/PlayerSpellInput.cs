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
    /// Selection is a shared index rather than an input mode of its own: the number row is read
    /// here, and <see cref="SpellWheelInput"/> drives the same <see cref="Select"/>. Either works at
    /// any time, and neither knows about the other.
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

        // Optional sibling. Resolved rather than serialized so a player without a wheel - a test
        // rig, an early tutorial - still casts.
        private SpellWheelInput wheel;

        /// <summary>Raised when the selected spell changes, for a HUD that does not exist yet.</summary>
        public event Action<SpellDefinition> SelectionChanged;

        public int SelectedIndex { get; private set; }

        public SpellDefinition SelectedSpell =>
            SelectedIndex >= 0 && SelectedIndex < knownSpells.Count ? knownSpells[SelectedIndex] : null;

        public IReadOnlyList<SpellDefinition> KnownSpells => knownSpells;

        private void Awake()
        {
            spellcaster = GetComponent<CharacterSpellcaster>();
            wheel = GetComponent<SpellWheelInput>();

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
                inputReader.CastReleased += OnCastReleased;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.CastPerformed -= OnCastPressed;
                inputReader.CastReleased -= OnCastReleased;
            }

            // Releasing on disable stops a channel surviving the component being switched off.
            // Scoped to the selected spell so it cannot drop a ward the block input is holding.
            spellcaster.ReleaseChannel(SelectedSpell);
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

        /// <summary>
        /// Set when a click was spent choosing a spell, so the matching release is not read as the
        /// end of a channel that never started.
        /// </summary>
        private bool castWasConsumedByWheel;

        private void OnCastPressed()
        {
            // Clicking a slot picks it. Checked here rather than by having the wheel subscribe to
            // the same button, because two handlers on one event resolve by subscription order and
            // that is not something a component can be sure of.
            if (wheel != null && wheel.IsOpen)
            {
                castWasConsumedByWheel = true;
                wheel.CloseAndCommit();
                return;
            }

            if (SelectedSpell == null)
            {
                GameLog.Warn(LogChannel.Magic, "Cast pressed with no spell selected.", this);
                return;
            }

            spellcaster.TryCast(SelectedSpell);
        }

        private void OnCastReleased()
        {
            if (castWasConsumedByWheel)
            {
                castWasConsumedByWheel = false;
                return;
            }

            spellcaster.ReleaseChannel(SelectedSpell);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// A single line, now that <c>SpellWheelInput</c> shows the full list on demand. Nine rows
        /// permanently on screen was a list nobody read once there was somewhere better to look.
        /// </summary>
        private void OnGUI()
        {
            var area = new Rect(10f, 337f, 420f, 40f);
            GUILayout.BeginArea(area, GUI.skin.box);

            SpellDefinition spell = SelectedSpell;

            if (spell == null)
            {
                GUILayout.Label("No spell selected.  Q for the wheel");
                GUILayout.EndArea();
                return;
            }

            float cooldown = spellcaster != null ? spellcaster.CooldownRemaining(spell) : 0f;
            string cost = spell.IsChannelled
                ? $"{spell.ManaCost:0} + {spell.ManaPerSecond:0}/s (hold)"
                : $"{spell.ManaCost:0} mana";
            string state = spellcaster != null && spellcaster.IsChannelling && spellcaster.CurrentSpell == spell
                ? "  CHANNELLING"
                : cooldown > 0f ? $"  cooling {cooldown:0.0}s" : string.Empty;

            GUILayout.Label($"{SelectedIndex + 1}. {spell.DisplayName}   {cost}{state}");
            GUILayout.Label("Q wheel (point, click or press a number)   LMB cast   RMB block   MMB/Tab lock on");
            GUILayout.EndArea();
        }
#endif
    }
}
