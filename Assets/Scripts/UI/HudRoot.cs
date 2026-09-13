using Frieren.Characters;
using Frieren.Characters.Targeting;
using Frieren.Core.Debugging;
using Frieren.Magic;
using Frieren.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Frieren.UI
{
    /// <summary>
    /// The player's heads-up display: vitals, the selected spell, and the lock-on reticle.
    /// </summary>
    /// <remarks>
    /// Built in code rather than authored as a prefab. A UGUI hierarchy in hand-written YAML is a
    /// Canvas, a scaler, nested RectTransforms with anchor and pivot arithmetic, and a font asset
    /// that does not exist in this project; every one of those is a GUID or a serialized field that
    /// cannot be checked without opening the editor, and this project has no editor in the loop.
    /// As C# it is compiled, and the layout is readable in one file.
    ///
    /// It is told, never asks. Health, mana and spell selection arrive as events, exactly as
    /// <c>ICharacterAnimation</c> receives locomotion. Cooldown and cast state are the two things
    /// polled, because they are continuous values with no event to carry them, and a bar that
    /// updates once per frame is what a cooldown sweep is.
    ///
    /// Binding is by retry rather than by a static event. The player is spawned at runtime, this
    /// lives in the Boot scene and survives scene loads, and a static hook on the spawner would be
    /// a singleton in all but name. A quarter-second poll while unbound costs nothing, reattaches
    /// itself after a scene change without knowing that one happened, and cannot leak a subscription
    /// to a destroyed object.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HudRoot : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Vector2 barSize = new Vector2(320f, 22f);

        [SerializeField] private Vector2 barCorner = new Vector2(34f, 34f);

        [Header("Feel")]
        [SerializeField]
        [Tooltip("Seconds the trailing bar waits before it starts closing the gap.")]
        private float ghostDelay = 0.35f;

        [SerializeField]
        [Tooltip("Fractions of the bar the trailing bar closes per second.")]
        private float ghostRate = 0.55f;

        [Header("Binding")]
        [SerializeField]
        [Tooltip("Seconds between attempts to find the player while nothing is bound.")]
        private float rebindInterval = 0.25f;

        private Canvas canvas;
        private Image healthFill;
        private Image healthGhost;
        private Text healthLabel;
        private Image manaFill;
        private Image manaGhost;
        private Text manaLabel;
        private Image spellIcon;
        private Image spellCooldown;
        private Text spellName;
        private Text castState;
        private RectTransform reticle;
        private Text reticleLabel;

        private float healthGhostValue = 1f;
        private float manaGhostValue = 1f;
        private float healthGhostDelay;
        private float manaGhostDelay;
        private float nextBindAttempt;

        private CharacterHealth health;
        private CharacterMana mana;
        private CharacterSpellcaster spellcaster;
        private PlayerSpellInput spells;
        private PlayerTargetLock targetLock;
        private UnityEngine.Camera view;

        private void Awake()
        {
            Build();
            SetBound(false);
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (health == null && Time.unscaledTime >= nextBindAttempt)
            {
                nextBindAttempt = Time.unscaledTime + Mathf.Max(0.05f, rebindInterval);
                TryBind();
            }

            if (health == null)
            {
                return;
            }

            TickBars(Time.unscaledDeltaTime);
            TickSpell();
            TickReticle();
        }

        // -------------------------------------------------------------------
        // Binding
        // -------------------------------------------------------------------

        private void TryBind()
        {
            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            GameObject player = spawner != null ? spawner.SpawnedPlayer : null;

            if (player == null)
            {
                return;
            }

            health = player.GetComponent<CharacterHealth>();
            mana = player.GetComponent<CharacterMana>();
            spellcaster = player.GetComponent<CharacterSpellcaster>();
            spells = player.GetComponent<PlayerSpellInput>();
            targetLock = player.GetComponent<PlayerTargetLock>();

            if (health == null)
            {
                GameLog.Warn(LogChannel.UI, "Found a player with no CharacterHealth; HUD stays hidden.", this);
                return;
            }

            health.Changed += OnHealthChanged;
            OnHealthChanged(health.Current, health.Max);
            healthGhostValue = BarSmoothing.Safe(health.Normalized);

            if (mana != null)
            {
                mana.Changed += OnManaChanged;
                OnManaChanged(mana.Current, mana.Max);
                manaGhostValue = BarSmoothing.Safe(mana.Normalized);
            }

            if (spells != null)
            {
                spells.SelectionChanged += OnSelectionChanged;
                OnSelectionChanged(spells.SelectedSpell);
            }

            SetBound(true);
            GameLog.Info(LogChannel.UI, "HUD bound to the player.", this);
        }

        private void Unbind()
        {
            if (health != null)
            {
                health.Changed -= OnHealthChanged;
            }

            if (mana != null)
            {
                mana.Changed -= OnManaChanged;
            }

            if (spells != null)
            {
                spells.SelectionChanged -= OnSelectionChanged;
            }

            health = null;
            mana = null;
            spellcaster = null;
            spells = null;
            targetLock = null;
        }

        /// <summary>
        /// Hides everything until there is a player. An empty HUD over a loading screen reads as a
        /// bug, and a health bar showing full when there is nobody to be healthy is worse.
        /// </summary>
        private void SetBound(bool bound)
        {
            if (canvas != null)
            {
                canvas.enabled = bound;
            }
        }

        // -------------------------------------------------------------------
        // Per-frame
        // -------------------------------------------------------------------

        private void TickBars(float deltaTime)
        {
            float target = BarSmoothing.Safe(health.Normalized);
            healthGhostValue = BarSmoothing.Step(healthGhostValue, target, deltaTime, ghostRate,
                ref healthGhostDelay);
            healthFill.fillAmount = target;
            healthGhost.fillAmount = healthGhostValue;

            if (mana == null)
            {
                return;
            }

            float manaTarget = BarSmoothing.Safe(mana.Normalized);
            manaGhostValue = BarSmoothing.Step(manaGhostValue, manaTarget, deltaTime, ghostRate,
                ref manaGhostDelay);
            manaFill.fillAmount = manaTarget;
            manaGhost.fillAmount = manaGhostValue;
        }

        private void TickSpell()
        {
            if (spells == null || spellcaster == null)
            {
                return;
            }

            SpellDefinition spell = spells.SelectedSpell;

            if (spell == null)
            {
                spellCooldown.fillAmount = 0f;
                castState.text = string.Empty;
                return;
            }

            // Swept as a fraction of the spell's own cooldown, so a long and a short cooldown both
            // read as "this much left" rather than as an absolute the player has to know.
            float remaining = spellcaster.CooldownRemaining(spell);
            spellCooldown.fillAmount = spell.Cooldown > 0f
                ? Mathf.Clamp01(remaining / spell.Cooldown)
                : 0f;

            castState.text = spellcaster.IsChannelling
                ? "channelling"
                : spellcaster.IsCasting
                    ? "casting"
                    : remaining > 0.05f
                        ? $"{remaining:0.0}s"
                        : string.Empty;
        }

        private void TickReticle()
        {
            if (targetLock == null || !targetLock.IsLocked)
            {
                reticle.gameObject.SetActive(false);
                return;
            }

            if (view == null)
            {
                view = UnityEngine.Camera.main;
            }

            if (view == null)
            {
                reticle.gameObject.SetActive(false);
                return;
            }

            LockOnTarget target = targetLock.Current;
            Vector3 screen = view.WorldToScreenPoint(target.AimPosition);

            // Behind the camera still returns coordinates, and they are mirrored nonsense.
            if (screen.z <= 0f)
            {
                reticle.gameObject.SetActive(false);
                return;
            }

            reticle.gameObject.SetActive(true);

            // Overlay canvases are in screen pixels, so the point needs no camera conversion, only
            // re-centring: the reticle's parent is anchored to the middle of the screen.
            reticle.anchoredPosition = new Vector2(screen.x - Screen.width * 0.5f,
                                                   screen.y - Screen.height * 0.5f);
            reticleLabel.text = target.DisplayName;
        }

        // -------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------

        private void OnHealthChanged(float current, float max)
        {
            healthLabel.text = $"{current:0} / {max:0}";
            healthGhostDelay = ghostDelay;
        }

        private void OnManaChanged(float current, float max)
        {
            manaLabel.text = $"{current:0} / {max:0}";
            manaGhostDelay = ghostDelay;
        }

        private void OnSelectionChanged(SpellDefinition spell)
        {
            spellName.text = spell != null ? spell.DisplayName : "no spell";

            if (spell != null && spell.Icon != null)
            {
                spellIcon.sprite = spell.Icon;
                spellIcon.color = Color.white;
            }
            else
            {
                // No art: a flat plate in the spell's own tint still tells the player the selection
                // changed, which an empty square does not.
                spellIcon.sprite = null;
                spellIcon.color = spell != null ? spell.WheelTint : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        // -------------------------------------------------------------------
        // Construction
        // -------------------------------------------------------------------

        private void Build()
        {
            canvas = HudBuilder.CreateCanvas("HUD", sortingOrder: 100);
            canvas.transform.SetParent(transform, false);
            Transform root = canvas.transform;

            HudBuilder.CreateBar(root, "Health", Vector2.zero, barCorner + new Vector2(0f, barSize.y + 8f),
                barSize, new Color(0.78f, 0.28f, 0.30f), new Color(0.95f, 0.85f, 0.55f, 0.85f),
                out healthFill, out healthGhost, out healthLabel);

            HudBuilder.CreateBar(root, "Mana", Vector2.zero, barCorner, barSize,
                new Color(0.32f, 0.52f, 0.86f), new Color(0.70f, 0.82f, 0.98f, 0.8f),
                out manaFill, out manaGhost, out manaLabel);

            BuildSpellSlot(root);
            BuildReticle(root);
        }

        private void BuildSpellSlot(Transform root)
        {
            var corner = new Vector2(1f, 0f);
            Image plate = HudBuilder.CreateImage(root, "SpellSlot", new Color(0.05f, 0.06f, 0.09f, 0.78f));
            HudBuilder.Place(plate, corner, new Vector2(-34f, 34f), new Vector2(86f, 86f));

            spellIcon = HudBuilder.CreateImage(plate.transform, "Icon", Color.white);
            HudBuilder.Stretch(spellIcon, 5f);
            spellIcon.preserveAspect = true;

            // Radial sweep over the icon. Clockwise from the top, which is the direction every
            // cooldown in every game turns, so it needs no explaining.
            spellCooldown = HudBuilder.CreateImage(plate.transform, "Cooldown", new Color(0f, 0f, 0f, 0.68f));
            HudBuilder.Stretch(spellCooldown, 5f);
            spellCooldown.type = Image.Type.Filled;
            spellCooldown.fillMethod = Image.FillMethod.Radial360;
            spellCooldown.fillOrigin = (int)Image.Origin360.Top;
            spellCooldown.fillClockwise = true;
            spellCooldown.fillAmount = 0f;

            spellName = HudBuilder.CreateText(plate.transform, "Name", 16, TextAnchor.UpperCenter, Color.white);
            HudBuilder.Place(spellName, new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(200f, 20f));

            castState = HudBuilder.CreateText(plate.transform, "State", 15, TextAnchor.LowerCenter,
                new Color(0.95f, 0.88f, 0.6f));
            HudBuilder.Place(castState, new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(200f, 20f));
        }

        private void BuildReticle(Transform root)
        {
            Image marker = HudBuilder.CreateImage(root, "Reticle", new Color(1f, 0.55f, 0.35f, 0.9f));
            reticle = HudBuilder.Place(marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            marker.type = Image.Type.Filled;
            marker.fillMethod = Image.FillMethod.Radial360;
            marker.fillAmount = 1f;

            reticleLabel = HudBuilder.CreateText(marker.transform, "Name", 14, TextAnchor.UpperCenter,
                new Color(1f, 0.72f, 0.55f));
            HudBuilder.Place(reticleLabel, new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(220f, 18f));

            marker.gameObject.SetActive(false);
        }
    }
}
