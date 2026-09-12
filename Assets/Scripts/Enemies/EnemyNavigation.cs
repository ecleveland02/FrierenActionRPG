using UnityEngine;
using UnityEngine.AI;

namespace Frieren.Enemies
{
    /// <summary>Optional path steering. CharacterMotor remains the only movement integrator.</summary>
    public sealed class EnemyNavigation : MonoBehaviour
    {
        private NavMeshPath path;
        private readonly Vector3[] corners = new Vector3[32];
        private int cornerCount;
        private int corner;
        private float nextPathAt;

        private void Awake() => path = new NavMeshPath();

        public Vector3 DirectionTo(Vector3 destination)
        {
            if (Time.time >= nextPathAt)
            {
                nextPathAt = Time.time + 0.5f;
                cornerCount = 0;
                corner = 0;
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit start, 2f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(destination, out NavMeshHit end, 2f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path))
                {
                    cornerCount = path.GetCornersNonAlloc(corners);
                    corner = cornerCount > 1 ? 1 : 0;
                }
            }
            while (corner < cornerCount)
            {
                Vector3 delta = corners[corner] - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.16f) return delta.normalized;
                corner++;
            }
            return Vector3.zero;
        }
    }
}
