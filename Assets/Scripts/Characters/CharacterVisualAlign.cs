using Frieren.Core.Debugging;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// Puts a character's feet on the bottom of its collider, whatever the model's origin is.
    /// </summary>
    /// <remarks>
    /// A <c>CharacterController</c> stands on its transform's position: the capsule runs from there
    /// upwards. A model's origin is wherever whoever exported it decided, which for a rig authored
    /// in Blender is often the hips and occasionally the world origin of whatever scene it came out
    /// of. When the two disagree the character wades through the floor or hovers above it, and the
    /// usual fix is somebody typing a number into a transform until it looks right.
    ///
    /// That number is worth measuring instead of guessing, because it is different for every model
    /// and silently wrong after every model swap. This measures the rendered bounds once and moves
    /// the visual so its lowest point sits at the collider's base. Swapping the character later
    /// costs nothing; there is no offset baked into a prefab to remember to change.
    ///
    /// It logs the offset it applied. Once that number is known and stable it can be baked into the
    /// prefab and this component switched off, which is cheaper at runtime and easier to reason
    /// about; until then, measuring beats a guess made from a screenshot.
    ///
    /// Bounds, not the skeleton. A foot bone is not the lowest point of a character wearing boots,
    /// and a rig with no foot bone at all still has to stand on the ground.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterVisualAlign : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Measure and correct once at Start. Turn off after baking the offset into the prefab.")]
        private bool alignOnStart = true;

        [SerializeField]
        [Tooltip("Added on top of the measured correction. Positive lifts the model.")]
        private float extraLift;

        [SerializeField]
        [Tooltip("Corrections larger than this are reported as an error rather than applied " +
                 "silently, because they usually mean the wrong model or the wrong import scale.")]
        private float suspiciousAbove = 3f;

        /// <summary>How far the visual was moved. Zero before <see cref="Align"/> has run.</summary>
        public float AppliedOffset { get; private set; }

        private void Start()
        {
            if (alignOnStart)
            {
                Align();
            }
        }

        /// <summary>Measures the body and drops it onto the collider's base.</summary>
        public void Align()
        {
            var animator = GetComponentInChildren<Animator>();
            Transform visual = animator != null ? animator.transform : FirstRenderedChild();

            if (visual == null || visual == transform)
            {
                GameLog.Warn(LogChannel.Player,
                    $"{name}: nothing to align. No child carries an Animator or a Renderer.", this);
                return;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                GameLog.Warn(LogChannel.Player, $"{name}: the visual child has no renderers to measure.", this);
                return;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // The collider's base is this transform's own position; that is the contract a
            // CharacterController works to.
            float correction = transform.position.y - bounds.min.y + extraLift;

            if (Mathf.Abs(correction) < 0.001f)
            {
                GameLog.Info(LogChannel.Player, $"{name}: visual already aligned, nothing to do.", this);
                return;
            }

            if (Mathf.Abs(correction) > suspiciousAbove)
            {
                GameLog.Error(LogChannel.Player,
                    $"{name}: the body would need moving {correction:0.00}m to stand on its collider, " +
                    $"which is too far to be an origin offset. Bounds are {bounds.size} centred on " +
                    $"{bounds.center}. Check the model's import scale before trusting this.", this);
                return;
            }

            visual.position += Vector3.up * correction;
            AppliedOffset = correction;

            GameLog.Info(LogChannel.Player,
                $"{name}: lifted the body by {correction:0.000}m so its feet sit on the collider. " +
                $"Bake that into the prefab and switch alignOnStart off once it stops changing.", this);
        }

        private Transform FirstRenderedChild()
        {
            var renderer = GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.transform : null;
        }
    }
}
