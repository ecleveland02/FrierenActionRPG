using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// The arithmetic behind <see cref="CharacterMotor"/>, kept separate so it can be tested.
    /// </summary>
    /// <remarks>
    /// None of this needs a scene, a CharacterController or a frame. Pulling it out of the
    /// MonoBehaviour is what makes gravity and jump height verifiable in an edit-mode test instead
    /// of by feel.
    /// </remarks>
    public static class MotorMath
    {
        /// <summary>
        /// Initial upward speed needed to reach <paramref name="peakHeight"/> under a gravity of
        /// <paramref name="gravity"/> (expressed as a positive magnitude).
        /// </summary>
        public static float JumpSpeedForHeight(float peakHeight, float gravity)
        {
            if (peakHeight <= 0f || gravity <= 0f)
            {
                return 0f;
            }

            return Mathf.Sqrt(2f * gravity * peakHeight);
        }

        /// <summary>
        /// Integrates one step of gravity, clamped so a long fall cannot tunnel through colliders.
        /// </summary>
        public static float ApplyGravity(float verticalVelocity, float gravity, float terminalSpeed, float deltaTime)
        {
            float next = verticalVelocity - gravity * deltaTime;
            return Mathf.Max(next, -Mathf.Abs(terminalSpeed));
        }

        /// <summary>
        /// Moves <paramref name="current"/> toward <paramref name="target"/> at a fixed rate, so
        /// acceleration is expressed in units per second rather than as a frame-rate dependent lerp.
        /// </summary>
        public static Vector3 MoveTowardsRate(Vector3 current, Vector3 target, float unitsPerSecond, float deltaTime)
        {
            if (unitsPerSecond <= 0f)
            {
                return target;
            }

            return Vector3.MoveTowards(current, target, unitsPerSecond * deltaTime);
        }

        /// <summary>
        /// Converts a 2D input axis and a camera orientation into a world-space direction on the
        /// ground plane. Returns zero when the camera is looking straight down, where "forward"
        /// has no meaningful projection.
        /// </summary>
        public static Vector3 CameraRelativeDirection(Vector2 input, Quaternion cameraRotation)
        {
            if (input.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 forward = cameraRotation * Vector3.forward;
            Vector3 right = cameraRotation * Vector3.right;

            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                // Camera is looking near-vertically; fall back to its up vector flattened.
                forward = cameraRotation * Vector3.up;
                forward.y = 0f;

                if (forward.sqrMagnitude <= 0.0001f)
                {
                    return Vector3.zero;
                }
            }

            forward.Normalize();
            right.Normalize();

            Vector3 direction = forward * input.y + right * input.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }
    }
}
