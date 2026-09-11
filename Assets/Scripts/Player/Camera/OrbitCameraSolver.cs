using UnityEngine;

namespace Frieren.Player.Cameras
{
    /// <summary>
    /// Yaw/pitch orbit state and the positions derived from it. No scene, no transform, no input.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="OrbitCameraRig"/> so the parts that are easy to get subtly wrong -
    /// pitch clamping, yaw wrapping, where the camera actually ends up - can be tested directly.
    /// The rig is then only responsible for reading input, smoothing and collision.
    /// </remarks>
    public sealed class OrbitCameraSolver
    {
        private float minPitch;
        private float maxPitch;

        public OrbitCameraSolver(float minPitch = -35f, float maxPitch = 70f)
        {
            SetPitchLimits(minPitch, maxPitch);
        }

        /// <summary>Degrees, wrapped to (-180, 180].</summary>
        public float Yaw { get; private set; }

        /// <summary>Degrees, clamped to the configured limits. Positive looks down.</summary>
        public float Pitch { get; private set; }

        public float MinPitch => minPitch;

        public float MaxPitch => maxPitch;

        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);

        public void SetPitchLimits(float min, float max)
        {
            minPitch = Mathf.Min(min, max);
            maxPitch = Mathf.Max(min, max);
            Pitch = Mathf.Clamp(Pitch, minPitch, maxPitch);
        }

        /// <summary>
        /// Applies a look delta in degrees. Y is inverted so pushing up looks up, which is the
        /// convention players expect before they go and change it in options.
        /// </summary>
        public void ApplyLook(Vector2 degreesDelta)
        {
            Yaw = WrapAngle(Yaw + degreesDelta.x);
            Pitch = Mathf.Clamp(Pitch - degreesDelta.y, minPitch, maxPitch);
        }

        public void SetOrientation(float yaw, float pitch)
        {
            Yaw = WrapAngle(yaw);
            Pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        /// <summary>Where the camera sits when nothing is in the way.</summary>
        public Vector3 DesiredPosition(Vector3 pivot, float distance)
        {
            return pivot - Rotation * Vector3.forward * distance;
        }

        /// <summary>
        /// Keeps an angle in (-180, 180]. Without this, yaw accumulates without bound and loses
        /// float precision during a long session.
        /// </summary>
        public static float WrapAngle(float degrees)
        {
            degrees %= 360f;

            if (degrees > 180f)
            {
                degrees -= 360f;
            }
            else if (degrees <= -180f)
            {
                degrees += 360f;
            }

            return degrees;
        }
    }
}
