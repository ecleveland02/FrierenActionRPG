using Frieren.Core.Services;
using Frieren.Core.Timing;
using Frieren.Enemies;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>
    /// Shows what an enemy is about to do, and what it is doing.
    /// </summary>
    /// <remarks>
    /// The most important component in this assembly. <c>EnemyMelee</c> has a 0.55 second wind-up
    /// that exists so the player can react to it, and until something draws that wind-up it is not
    /// a dodge window - it is a coin flip followed by damage. Every judgement about whether the
    /// fight is fun depends on this being visible.
    ///
    /// It also makes the brain diagnosable. If the enemy never swings, the telegraph says whether
    /// it reached Attack at all, which separates a perception problem from a melee-timing one
    /// without reading a log.
    /// </remarks>
    [RequireComponent(typeof(EnemyBrain))]
    [DisallowMultipleComponent]
    public sealed class EnemyCombatFeedback : MonoBehaviour
    {
        [Header("Telegraph")]
        [SerializeField]
        [Tooltip("Held for the whole wind-up. This is the player's cue to move.")]
        private Color windUpColour = new Color(1f, 0.55f, 0.1f);

        [SerializeField]
        [Tooltip("The instant the swing lands.")]
        private Color strikeColour = new Color(1f, 0.15f, 0.1f);

        [Header("States")]
        [SerializeField] private Color alertedColour = new Color(1f, 0.85f, 0.3f);

        [SerializeField] private Color staggeredColour = new Color(0.6f, 0.5f, 1f);

        [SerializeField]
        [Tooltip("Show a faint colour while merely chasing, not only while swinging.")]
        private bool showChaseState = true;

        [Header("Impact")]
        [SerializeField]
        [Tooltip("Seconds of time dip when a swing connects. Zero disables it.")]
        private float hitStopSeconds = 0.06f;

        private EnemyBrain brain;
        private EnemyMelee melee;
        private CharacterFlash flash;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            melee = GetComponent<EnemyMelee>();
            flash = GetComponent<CharacterFlash>();
        }

        private void OnEnable()
        {
            brain.StateChanged += OnStateChanged;

            if (melee != null)
            {
                melee.SwingStarted += OnSwingStarted;
                melee.SwingLanded += OnSwingLanded;
            }
        }

        private void OnDisable()
        {
            brain.StateChanged -= OnStateChanged;

            if (melee != null)
            {
                melee.SwingStarted -= OnSwingStarted;
                melee.SwingLanded -= OnSwingLanded;
            }
        }

        private void Update()
        {
            // Driven every frame rather than only on the event, because the wind-up ends without
            // one - the melee component moves to its strike phase on its own clock.
            if (melee != null && melee.IsWindingUp)
            {
                flash?.SetSustained(windUpColour);
            }
            else if (brain.State == EnemyState.Stagger)
            {
                flash?.SetSustained(staggeredColour);
            }
            else if (showChaseState && brain.State != EnemyState.Idle && brain.State != EnemyState.Dead)
            {
                flash?.SetSustained(alertedColour);
            }
            else
            {
                flash?.ClearSustained();
            }
        }

        private void OnStateChanged(EnemyState previous, EnemyState current)
        {
            if (current == EnemyState.Dead)
            {
                flash?.ClearSustained();
            }
        }

        private void OnSwingStarted() => flash?.SetSustained(windUpColour);

        private void OnSwingLanded(int targetsHit)
        {
            flash?.Flash(strikeColour, 0.12f);

            // Only a connecting swing dips time. Dipping on a miss would reward the enemy for
            // swinging at nothing.
            if (targetsHit > 0 && hitStopSeconds > 0f && ServiceLocator.TryGet(out TimeScaleService time))
            {
                time.RequestDip(0.12f, hitStopSeconds);
            }
        }
    }
}
