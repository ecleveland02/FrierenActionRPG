using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Player.Cameras
{
    /// <summary>
    /// Chooses which target to lock onto, and which one to move to next. Points in, index out.
    /// </summary>
    /// <remarks>
    /// Deliberately knows nothing about components, scenes or physics: it takes world positions and
    /// returns an index into them. Target picking is the part of lock-on that feels wrong in ways
    /// that are hard to describe and harder to reproduce - it grabbed the one behind the pillar, it
    /// refused to move to the obvious one on the right - and none of that is diagnosable by playing
    /// the game repeatedly. As plain functions it is diagnosable by writing down the positions.
    ///
    /// Both functions return -1 rather than throwing when there is no answer, because "nothing
    /// worth locking onto" is the normal case, not an error.
    /// </remarks>
    public static class LockOnPicker
    {
        /// <summary>
        /// The best target to acquire from a standing start.
        /// </summary>
        /// <remarks>
        /// Scored on angle from where the camera is already looking, with distance as a tie-break
        /// rather than a competitor. Sorting by distance alone is the obvious implementation and is
        /// the wrong one: it locks onto whatever is nearest even when that is behind the player and
        /// off screen, which reads as the button being broken. Weighting angle heavily means the
        /// lock lands on what the player was already looking at, which is what they meant.
        /// </remarks>
        /// <param name="points">Candidate aim positions.</param>
        /// <param name="eye">Where the camera is looking from.</param>
        /// <param name="forward">Where it is looking. Need not be normalised.</param>
        /// <param name="maxDistance">Beyond this, a target is not offered at all.</param>
        /// <param name="maxAngle">Degrees off <paramref name="forward"/> before a target is ignored.</param>
        /// <param name="distanceWeight">Degrees of angle one metre of distance is worth.</param>
        /// <returns>Index into <paramref name="points"/>, or -1.</returns>
        public static int Best(IReadOnlyList<Vector3> points, Vector3 eye, Vector3 forward,
            float maxDistance, float maxAngle, float distanceWeight = 1.5f)
        {
            if (points == null || points.Count == 0)
            {
                return -1;
            }

            Vector3 facing = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            int best = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 offset = points[i] - eye;
                float distance = offset.magnitude;

                if (distance > maxDistance || distance <= 0.0001f)
                {
                    continue;
                }

                float angle = Vector3.Angle(facing, offset / distance);

                if (angle > maxAngle)
                {
                    continue;
                }

                float score = angle + distance * distanceWeight;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// The next target to the left or right of the current one, for flicking between them.
        /// </summary>
        /// <remarks>
        /// Sides are decided in screen space, not world space: what the player means by "the one on
        /// the right" is the one further right on their monitor. Projecting each candidate onto the
        /// camera's right axis and comparing against the current target gives exactly that, and
        /// keeps working when the player is looking straight down.
        ///
        /// The nearest one on the requested side wins, measured along that same axis, so a flick
        /// steps through a row of enemies one at a time instead of jumping to the far end.
        /// </remarks>
        /// <param name="sign">Positive for right, negative for left.</param>
        public static int NextInDirection(IReadOnlyList<Vector3> points, int current, Vector3 eye,
            Vector3 right, float sign, float maxDistance)
        {
            if (points == null || points.Count == 0 || current < 0 || current >= points.Count)
            {
                return -1;
            }

            Vector3 axis = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            float direction = sign >= 0f ? 1f : -1f;
            float currentSide = Vector3.Dot(points[current] - eye, axis);

            int best = -1;
            float bestGap = float.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                if (i == current)
                {
                    continue;
                }

                Vector3 offset = points[i] - eye;

                if (offset.magnitude > maxDistance)
                {
                    continue;
                }

                float gap = (Vector3.Dot(offset, axis) - currentSide) * direction;

                // Level with the current target is not "further along": without this, two enemies
                // at the same screen x would swap back and forth on every flick.
                if (gap <= 0.01f)
                {
                    continue;
                }

                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }

            return best;
        }
    }
}
