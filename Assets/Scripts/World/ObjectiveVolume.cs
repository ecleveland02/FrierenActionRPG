using Frieren.Core;
using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// Completes an objective when the player walks into it.
    /// </summary>
    /// <remarks>
    /// A trigger rather than a check on the thing that was supposed to be solved, and that is the
    /// point: the goal is "get into the courtyard", not "unlock the gate". A player who burned the
    /// gate, or levitated over the wall and never touched it, has done the same thing and the level
    /// should agree. Tying progress to one solution would quietly make the other answers wrong.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class ObjectiveVolume : MonoBehaviour
    {
        [SerializeField] private SliceObjectives objectives;

        [SerializeField]
        [Tooltip("Which goal this completes. Must match an id on the objectives component.")]
        private string goalId;

        [SerializeField]
        [Tooltip("Who counts as arriving. The player, normally.")]
        private LayerMask triggeredBy = GameLayers.PlayerMask;

        [SerializeField]
        [Tooltip("Switch the volume off once it has fired, since it can only happen once.")]
        private bool once = true;

        private void Reset()
        {
            // Only meaningful when added in the editor: a solid volume would be a wall.
            var attached = GetComponent<Collider>();

            if (attached != null)
            {
                attached.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (objectives == null || other == null)
            {
                return;
            }

            if ((triggeredBy.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            objectives.Complete(goalId);

            if (once)
            {
                gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
#endif
    }
}
