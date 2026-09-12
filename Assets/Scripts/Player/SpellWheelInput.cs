using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Core.Services;
using Frieren.Core.Timing;
using Frieren.Magic;
using Frieren.Player.Cameras;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Player
{
    /// <summary>
    /// A radial spell selector: hold the wheel button, point or press a number, let go to commit.
    /// </summary>
    /// <remarks>
    /// Selection used to be the number row alone, which stops scaling somewhere around five spells
    /// and stopped being usable at nine. A wheel shows every option at once and puts them somewhere
    /// the hand can learn, which a row of digits never does.
    ///
    /// Pointer and number row both work, and neither is a special case: the wheel tracks a
    /// <see cref="Highlighted"/> index, the pointer moves it by angle, the number row sets it
    /// directly, and releasing commits whatever it lands on. Pressing a number while the wheel is
    /// open therefore just works, and so does pressing one while it is closed.
    ///
    /// It takes the pointer from the camera while it is open, through
    /// <see cref="OrbitCameraRig.LookEnabled"/>. Aiming a wheel and turning the camera with the
    /// same mouse movement is not a thing that can be shared.
    ///
    /// It also files a claim with <c>CursorService</c>, which is what puts the real mouse cursor
    /// back on screen so slots can be clicked. While that claim stands the wheel reads the
    /// pointer's actual position rather than accumulating deltas, because a visible cursor that
    /// does not agree with the slot being highlighted is worse than no cursor. The cursor is warped
    /// to the middle of the wheel on open so the two always start out agreeing.
    ///
    /// It also slows time while it is open, through <c>TimeScaleService</c>. That is what makes the
    /// wheel usable mid-fight rather than a thing you only open when nothing is happening - which
    /// would defeat the point of putting eight spells behind one button. It is a claim with an
    /// owner, so the wheel releases its own slow and never anybody else's, and a missing time
    /// service simply means the wheel opens at normal speed.
    ///
    /// Nothing is committed until release. Sweeping across the wheel does not fire seven spell
    /// changes on the way to the one you meant, and returning to the dead zone in the middle falls
    /// back to what was already selected, so opening the wheel and letting go changes nothing.
    /// </remarks>
    [RequireComponent(typeof(PlayerSpellInput))]
    [DisallowMultipleComponent]
    public sealed class SpellWheelInput : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;

        [SerializeField]
        [Tooltip("Pointer travel, in pixels, before the wheel starts tracking. Stops a twitch from re-selecting.")]
        private float deadZone = 40f;

        [SerializeField]
        [Tooltip("Radius of the drawn wheel, in pixels.")]
        private float radius = 150f;

        [SerializeField]
        [Tooltip("How far the pointer can travel from the centre. Beyond this it simply clamps.")]
        private float pointerRange = 220f;

        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("How slowly time runs while the wheel is open. 1 disables the slow entirely.")]
        private float timeScaleWhileOpen = 0.25f;

        private PlayerSpellInput spells;
        private OrbitCameraRig cameraRig;
        private Vector2 pointer;
        private bool cameraLookWasEnabled = true;
        private bool holdingCursor;

        /// <summary>What the number row last chose. The pointer overrides it while it is out of the
        /// dead zone, and it is what the wheel falls back to when the pointer is not.</summary>
        private int keyHighlight;

        private int lastSeenSelection;

        /// <summary>True while the wheel is up.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// The slot the wheel would commit right now. -1 only when there is nothing to choose from.
        /// </summary>
        public int Highlighted { get; private set; } = -1;

        private void Awake()
        {
            spells = GetComponent<PlayerSpellInput>();

            if (inputReader == null)
            {
                GameLog.Error(LogChannel.Magic,
                    $"{name}: SpellWheelInput has no InputReader, so the wheel will never open.", this);
            }
        }

        /// <summary>Told by the spawner, the same way locomotion and the spellcaster are.</summary>
        public void SetCameraRig(OrbitCameraRig rig) => cameraRig = rig;

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.SpellWheelPerformed += Open;
                inputReader.SpellWheelReleased += CloseAndCommit;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.SpellWheelPerformed -= Open;
                inputReader.SpellWheelReleased -= CloseAndCommit;
            }

            // Leaving the camera switched off because a component was disabled mid-wheel would be
            // an unrecoverable state with no obvious cause.
            Close();
        }

        public void Open()
        {
            if (IsOpen || spells.KnownSpells.Count == 0)
            {
                return;
            }

            IsOpen = true;
            pointer = Vector2.zero;
            keyHighlight = spells.SelectedIndex;
            lastSeenSelection = spells.SelectedIndex;
            Highlighted = keyHighlight;

            if (cameraRig != null)
            {
                cameraLookWasEnabled = cameraRig.LookEnabled;
                cameraRig.LookEnabled = false;
            }

            if (timeScaleWhileOpen < 1f && ServiceLocator.TryGet(out TimeScaleService time))
            {
                time.TryHold(this, timeScaleWhileOpen);
            }

            ClaimCursor();
        }

        /// <summary>
        /// Puts the hardware cursor back on screen, in the middle of the wheel.
        /// </summary>
        /// <remarks>
        /// Warping matters as much as unhiding. The cursor was locked to the centre while it was
        /// hidden, but the operating system remembers where it was before that, and an unhidden
        /// cursor that reappears in the corner of the screen highlights a slot the player never
        /// pointed at. Releasing the claim on close re-locks and re-hides it.
        /// </remarks>
        private void ClaimCursor()
        {
            if (!ServiceLocator.TryGet(out CursorService cursor))
            {
                return;
            }

            holdingCursor = cursor.RequestPointer(this);

            if (holdingCursor && Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }
        }

        /// <summary>Closes without changing the selection.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Highlighted = -1;

            if (cameraRig != null)
            {
                cameraRig.LookEnabled = cameraLookWasEnabled;
            }

            // By owner, so closing this wheel cannot cancel a slow that belongs to something else.
            if (ServiceLocator.TryGet(out TimeScaleService time))
            {
                time.ReleaseHold(this);
            }

            if (holdingCursor && ServiceLocator.TryGet(out CursorService cursor))
            {
                cursor.ReleasePointer(this);
            }

            holdingCursor = false;
        }

        public void CloseAndCommit()
        {
            if (!IsOpen)
            {
                return;
            }

            int chosen = Highlighted;
            Close();

            if (chosen >= 0)
            {
                spells.Select(chosen);
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // A number pressed while the wheel is open takes over, and re-centres the pointer so
            // the wheel does not snap straight back to wherever the mouse happened to be resting.
            // PlayerSpellInput reads the row itself; this only notices that it moved.
            if (spells.SelectedIndex != lastSeenSelection)
            {
                lastSeenSelection = spells.SelectedIndex;
                keyHighlight = lastSeenSelection;
                pointer = Vector2.zero;
            }

            TrackPointer();
        }

        private void TrackPointer()
        {
            if (inputReader == null)
            {
                return;
            }

            Vector2 look = inputReader.LookInput;

            if (holdingCursor && Mouse.current != null)
            {
                // The cursor is visible, so it is the truth. Accumulating deltas alongside a
                // rendered pointer lets the two drift apart, and then the wheel highlights one
                // slot while the arrow sits over another.
                Vector2 screen = Mouse.current.position.ReadValue();
                pointer = screen - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
            else if (inputReader.LookIsPointerDelta)
            {
                pointer += new Vector2(look.x, look.y);
            }
            else
            {
                // A stick reports a position, not a movement, so it points straight at a slot
                // rather than dragging towards one.
                pointer = look * pointerRange;
            }

            pointer = Vector2.ClampMagnitude(pointer, pointerRange);

            int slot = pointer.magnitude < deadZone
                ? -1
                : SlotFor(pointer, spells.KnownSpells.Count);

            // The pointer wins while it is pointing somewhere. In the dead zone the wheel keeps
            // whatever was already chosen, so opening it and letting go is not a way to lose your
            // spell.
            Highlighted = slot >= 0 ? slot : keyHighlight;
        }

        /// <summary>
        /// Which slot a direction points at. Slot 0 sits at the top and they run clockwise, because
        /// that is the order the number row is already in.
        /// </summary>
        public static int SlotFor(Vector2 direction, int slotCount)
        {
            if (slotCount <= 0 || direction.sqrMagnitude <= 0.0001f)
            {
                return -1;
            }

            float degrees = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

            if (degrees < 0f)
            {
                degrees += 360f;
            }

            float slice = 360f / slotCount;

            // Offset by half a slice so a slot is centred on its angle rather than starting at it.
            int slot = Mathf.FloorToInt((degrees + slice * 0.5f) / slice);
            return slot % slotCount;
        }

        /// <summary>The screen offset of a slot's centre, for drawing. Same convention as SlotFor.</summary>
        public static Vector2 SlotOffset(int slot, int slotCount, float radius)
        {
            if (slotCount <= 0)
            {
                return Vector2.zero;
            }

            float degrees = slot * (360f / slotCount);
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians) * radius, Mathf.Cos(radians) * radius);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float IconSize = 62f;

        private GUIStyle slotStyle;
        private GUIStyle centreStyle;

        private static Rect Grow(Rect rect, float by) =>
            new Rect(rect.x - by, rect.y - by, rect.width + by * 2f, rect.height + by * 2f);

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            slotStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            centreStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };

            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            int count = spells.KnownSpells.Count;

            for (int i = 0; i < count; i++)
            {
                SpellDefinition spell = spells.KnownSpells[i];

                if (spell == null)
                {
                    continue;
                }

                // Screen y grows downwards, so the offset's y is subtracted to put slot 0 on top.
                Vector2 offset = SlotOffset(i, count, radius);
                bool highlighted = i == Highlighted;
                float size = highlighted ? IconSize * 1.18f : IconSize;
                var box = new Rect(centre.x + offset.x - size * 0.5f,
                                   centre.y - offset.y - size * 0.5f, size, size);

                Color previous = GUI.color;

                if (spell.Icon != null)
                {
                    // The tint plate sits behind the art rather than on it. Multiplying a painted
                    // icon by a colour turns every spell into a wash of that colour and throws away
                    // the thing that made it recognisable.
                    GUI.color = highlighted
                        ? new Color(spell.WheelTint.r, spell.WheelTint.g, spell.WheelTint.b, 0.95f)
                        : new Color(spell.WheelTint.r * 0.45f, spell.WheelTint.g * 0.45f,
                                    spell.WheelTint.b * 0.45f, 0.7f);
                    GUI.DrawTexture(Grow(box, 4f), Texture2D.whiteTexture);

                    GUI.color = highlighted ? Color.white : new Color(1f, 1f, 1f, 0.72f);
                    GUI.DrawTexture(box, spell.Icon.texture, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    // No art yet. The name still has to be readable, or an unillustrated spell
                    // becomes an empty square nobody can identify.
                    GUI.color = highlighted ? new Color(1f, 0.9f, 0.5f) : new Color(1f, 1f, 1f, 0.75f);
                    GUI.Box(box, spell.DisplayName, slotStyle);
                }

                GUI.color = highlighted ? Color.white : new Color(1f, 1f, 1f, 0.8f);
                GUI.Label(new Rect(box.x, box.yMax + 1f, box.width, 16f), $"{i + 1}", slotStyle);
                GUI.color = previous;
            }

            // The centre carries the name and cost of whatever is selected, so the ring can be
            // pictures without the player having to memorise which picture costs what.
            SpellDefinition chosen = Highlighted >= 0 && Highlighted < count
                ? spells.KnownSpells[Highlighted]
                : null;

            string label = chosen != null
                ? $"{chosen.DisplayName}\n{chosen.ManaCost:0} mana"
                : "no spell";
            GUI.Label(new Rect(centre.x - 90f, centre.y - 18f, 180f, 36f), label, centreStyle);
        }
#endif
    }
}
