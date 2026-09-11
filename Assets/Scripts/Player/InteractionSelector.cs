using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Player
{
    /// <summary>
    /// Picks which of several nearby things the player most likely means to interact with.
    /// </summary>
    /// <remarks>
    /// Nearest-wins is wrong: standing between a door and a barrel, the player looking straight at
    /// the door expects the door. Most-centred-wins is also wrong: a distant object dead ahead
    /// beats a barrel at arm's length. Scoring both, each normalised against its own cut-off, gets
    /// the ordinary cases right and is honest about being a heuristic.
    /// </remarks>
    public static class InteractionSelector
    {
        /// <summary>
        /// Returns the index of the best candidate, or -1 when none qualifies.
        /// </summary>
        /// <param name="positions">Candidate world positions.</param>
        /// <param name="origin">Where the player is looking from.</param>
        /// <param name="forward">The player's facing. Need not be normalised; the Y component is ignored.</param>
        /// <param name="maxDistance">Beyond this, a candidate is rejected outright.</param>
        /// <param name="maxAngleDegrees">Off-axis angle beyond which a candidate is rejected.</param>
        /// <param name="angleWeight">
        /// Relative importance of aim over proximity. 1 weighs them equally; above 1 favours what
        /// the player is facing.
        /// </param>
        public static int SelectBestIndex(IReadOnlyList<Vector3> positions, Vector3 origin, Vector3 forward,
            float maxDistance, float maxAngleDegrees, float angleWeight = 1.5f)
        {
            if (positions == null || positions.Count == 0 || maxDistance <= 0f || maxAngleDegrees <= 0f)
            {
                return -1;
            }

            Vector3 facing = forward;
            facing.y = 0f;

            if (facing.sqrMagnitude <= 0.0001f)
            {
                return -1;
            }

            facing.Normalize();

            int bestIndex = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 toCandidate = positions[i] - origin;
                float distance = toCandidate.magnitude;

                if (distance > maxDistance)
                {
                    continue;
                }

                Vector3 flat = toCandidate;
                flat.y = 0f;

                // A candidate at the player's own feet has no meaningful direction; accept it on
                // distance alone rather than rejecting it for having an undefined angle.
                float angle = flat.sqrMagnitude <= 0.0001f ? 0f : Vector3.Angle(facing, flat.normalized);

                if (angle > maxAngleDegrees)
                {
                    continue;
                }

                float score = angleWeight * (angle / maxAngleDegrees) + distance / maxDistance;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
