using Frieren.Characters;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>
    /// Tips a body over and sinks it into the ground instead of letting it blink out of existence.
    /// </summary>
    /// <remarks>
    /// <c>EnemyBrain</c> disables the GameObject after its corpse duration, which is correct - the
    /// object should stop existing. This is only about the two seconds before that, because a
    /// character vanishing between frames reads as a bug even when it is deliberate, and a kill
    /// with no follow-through is hard to feel good about.
    ///
    /// It moves the visual child, never the root. The root carries the `CharacterController` and
    /// the colliders, and rotating those would have the corpse shoving the player around while it
    /// falls over.
    /// </remarks>
    [RequireComponent(typeof(CharacterHealth))]
    [DisallowMultipleComponent]
    public sealed class DeathSink : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("What tips and sinks. Defaults to the first child, which is the visual on every character here.")]
        private Transform visual;

        [SerializeField] private float tipDegrees = 82f;

        [SerializeField] private float sinkMetres = 1.6f;

        [SerializeField]
        [Tooltip("Seconds to fall over. Should be under the corpse duration or it never finishes.")]
        private float duration = 1.4f;

        private CharacterHealth health;
        private Vector3 restingPosition;
        private Quaternion restingRotation;
        private float startedAt = -1f;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();

            if (visual == null && transform.childCount > 0)
            {
                visual = transform.GetChild(0);
            }

            if (visual != null)
            {
                restingPosition = visual.localPosition;
                restingRotation = visual.localRotation;
            }
        }

        private void OnEnable()
        {
            health.Died += OnDied;
            RestoreUpright();
        }

        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(DamageInfo damage) => startedAt = Time.time;

        private void Update()
        {
            if (startedAt < 0f || visual == null)
            {
                return;
            }

            // Revive has no event of its own, and a character brought back while lying face-down
            // stays face-down otherwise. Cheap to check, and it is the debug revive key's problem
            // as much as anything's.
            if (health.IsAlive)
            {
                RestoreUpright();
                return;
            }

            float progress = duration <= 0f ? 1f : Mathf.Clamp01((Time.time - startedAt) / duration);

            // Tips first, then sinks. Both at once looks like the model is falling through the
            // floor rather than falling over on it.
            float tip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.45f));
            float sink = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.4f) / 0.6f));

            visual.localRotation = restingRotation * Quaternion.Euler(tipDegrees * tip, 0f, 0f);
            visual.localPosition = restingPosition - Vector3.up * (sinkMetres * sink);
        }

        /// <summary>
        /// Puts the body back upright. Called on enable so a pooled or revived character does not
        /// come back still lying down. Not named Reset, which Unity calls by itself.
        /// </summary>
        private void RestoreUpright()
        {
            startedAt = -1f;

            if (visual != null)
            {
                visual.localPosition = restingPosition;
                visual.localRotation = restingRotation;
            }
        }
    }
}
