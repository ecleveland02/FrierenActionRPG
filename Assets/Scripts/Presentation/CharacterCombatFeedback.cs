using Frieren.Characters;
using Frieren.Core.Services;
using Frieren.Core.Timing;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>
    /// Everything a character does to say it was hit, warded, healed or killed.
    /// </summary>
    /// <remarks>
    /// One component subscribing to the vitals events rather than a flash component, a numbers
    /// component and a shake component each subscribing separately. They all fire off the same two
    /// or three events and want to agree about what happened, so keeping the decision in one place
    /// means "a hit the barrier absorbed" is described once instead of three times, consistently.
    ///
    /// Every output it drives is optional. No flash shell, no numbers, no registered shake service
    /// or time service - each is simply skipped. A presentation layer that throws because something
    /// it wanted to decorate is absent has the dependency backwards.
    /// </remarks>
    [RequireComponent(typeof(CharacterHealth))]
    [DisallowMultipleComponent]
    public sealed class CharacterCombatFeedback : MonoBehaviour
    {
        private const float HealReportThreshold = 1f;

        [Header("Colours")]
        [SerializeField] private Color hurtColour = new Color(1f, 0.25f, 0.2f);

        [SerializeField] private Color wardedColour = new Color(0.45f, 0.75f, 1f);

        [SerializeField] private Color healColour = new Color(0.4f, 1f, 0.5f);

        [SerializeField] private Color deathColour = new Color(0.85f, 0.15f, 0.35f);

        [Header("Timing")]
        [SerializeField] private float flashSeconds = 0.16f;

        [Header("Impact")]
        [SerializeField]
        [Tooltip("Whether hits on this character shake the view. On for the player, off for enemies.")]
        private bool shakesTheView;

        [SerializeField]
        [Tooltip("Whether hits on this character dip time. Reserve it for the ones that matter.")]
        private bool stopsTime;

        [SerializeField]
        [Tooltip("Damage that counts as a heavy hit and gets the full kick.")]
        private float heavyDamage = 25f;

        private CharacterHealth health;
        private CharacterBarrier barrier;
        private CharacterFlash flash;
        private FloatingCombatText text;
        private float lastKnownHealth;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            barrier = GetComponent<CharacterBarrier>();
            flash = GetComponent<CharacterFlash>();
            text = GetComponent<FloatingCombatText>();
            lastKnownHealth = health.Current;
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.DamageReduced += OnDamageReduced;
            health.Died += OnDied;
            health.Changed += OnHealthChanged;

            if (barrier != null)
            {
                barrier.Raised += OnBarrierRaised;
                barrier.Broke += OnBarrierBroke;
            }
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.DamageReduced -= OnDamageReduced;
            health.Died -= OnDied;
            health.Changed -= OnHealthChanged;

            if (barrier != null)
            {
                barrier.Raised -= OnBarrierRaised;
                barrier.Broke -= OnBarrierBroke;
            }
        }

        private void OnDamaged(DamageInfo damage, float taken)
        {
            if (taken <= 0f)
            {
                return;
            }

            flash?.Flash(hurtColour, flashSeconds);
            text?.Show($"-{taken:0}", hurtColour);

            float weight = Mathf.Clamp01(taken / Mathf.Max(1f, heavyDamage));

            if (shakesTheView && ServiceLocator.TryGet(out IScreenShake shake))
            {
                shake.Shake(Mathf.Lerp(0.06f, 0.3f, weight), Mathf.Lerp(0.12f, 0.3f, weight));
            }

            if (stopsTime && ServiceLocator.TryGet(out TimeScaleService time))
            {
                // Short and shallow. A long hit-stop on every hit turns a fight into a slideshow;
                // this is only meant to give an impact somewhere to land.
                time.RequestDip(Mathf.Lerp(0.35f, 0.08f, weight), Mathf.Lerp(0.04f, 0.09f, weight));
            }
        }

        /// <summary>
        /// Raised when a modifier ate some or all of a hit. Distinguishing this from taking the
        /// damage is the whole reason the barrier is legible at all.
        /// </summary>
        private void OnDamageReduced(DamageInfo damage, float remaining)
        {
            float absorbed = damage.Amount - remaining;

            if (absorbed <= 0f)
            {
                return;
            }

            flash?.Flash(wardedColour, flashSeconds);
            text?.Show($"({absorbed:0})", wardedColour);
        }

        private void OnDied(DamageInfo damage)
        {
            flash?.Flash(deathColour, flashSeconds * 3f);
            text?.Show("DOWN", deathColour);

            if (ServiceLocator.TryGet(out IScreenShake shake))
            {
                shake.Shake(0.35f, 0.45f);
            }

            if (ServiceLocator.TryGet(out TimeScaleService time))
            {
                time.RequestDip(0.1f, 0.16f);
            }
        }

        private void OnBarrierRaised(float strength) => text?.Show("WARD", wardedColour);

        private void OnBarrierBroke()
        {
            flash?.Flash(wardedColour, flashSeconds * 2f);
            text?.Show("BROKEN", wardedColour);
        }

        /// <summary>
        /// Health has no "healed" event, so healing is read off the pool moving upwards.
        /// </summary>
        /// <remarks>
        /// The threshold is what makes this usable: regeneration arrives as a fraction of a point
        /// every frame, and reporting each one would bury a real heal under a hundred +0s. Adding a
        /// <c>Healed</c> event to <c>CharacterHealth</c> would be tidier, but a presentation layer
        /// should not be reshaping gameplay's API for its own convenience when it can derive what
        /// it needs.
        /// </remarks>
        private void OnHealthChanged(float current, float max)
        {
            float gained = current - lastKnownHealth;
            lastKnownHealth = current;

            if (gained < HealReportThreshold)
            {
                return;
            }

            flash?.Flash(healColour, flashSeconds);
            text?.Show($"+{gained:0}", healColour);
        }
    }
}
