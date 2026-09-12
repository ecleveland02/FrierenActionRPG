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
        /// Slides the pivot sideways and up in the camera's own frame, which pushes the subject the
        /// other way on screen.
        /// </summary>
        /// <remarks>
        /// This is how the character ends up below the middle of the screen rather than dead centre.
        /// The pivot is what projects to the centre, so lifting it lets the character sit low, which
        /// is worth doing for a game about aiming spells: a body in the middle of the screen covers
        /// the thing you are aiming at.
        ///
        /// The offset moves the pivot rather than the camera on purpose. Everything downstream -
        /// the orbit, the collision sweep - is expressed relative to the pivot, so offsetting the
        /// camera afterwards would leave the sweep testing a line the camera no longer travels
        /// along, and the camera would clip the walls the sweep thought it had avoided.
        ///
        /// Aim is unaffected: casts follow the camera's forward, which still passes through the
        /// centre of the screen, so the crosshair keeps meaning what it meant.
        /// </remarks>
        public Vector3 FramedPivot(Vector3 pivot, Vector2 framingOffset)
        {
            if (framingOffset == Vector2.zero)
            {
                return pivot;
            }

            return pivot + Rotation * new Vector3(framingOffset.x, framingOffset.y, 0f);
        }

        /// <summary>
        /// The yaw and pitch that look from one point at another, in this solver's convention.
        /// </summary>
        /// <returns>x is yaw, y is pitch. Pitch is positive looking down, matching <see cref="Pitch"/>.</returns>
        public static Vector2 LookAngles(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float flat = new Vector2(offset.x, offset.z).magnitude;

            // Straight up or down has no meaningful yaw. Returning zero would snap the camera to
            // north; the caller keeps its current yaw instead by getting NaN-free zero pitch here.
            if (flat <= 0.0001f)
            {
                return new Vector2(0f, offset.y >= 0f ? -89f : 89f);
            }

            float yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(offset.y, flat) * Mathf.Rad2Deg;
            return new Vector2(WrapAngle(yaw), pitch);
        }

        /// <summary>
        /// Eases the orbit toward an orientation. <paramref name="t"/> is a 0..1 blend, already
        /// frame-rate corrected by the caller.
        /// </summary>
        public void BlendTowards(float yaw, float pitch, float t)
        {
            t = Mathf.Clamp01(t);

            // LerpAngle rather than Lerp, so blending from 170 to -170 crosses 20 degrees of the
            // short way round instead of sweeping 340 degrees the long way.
            SetOrientation(Mathf.LerpAngle(Yaw, yaw, t), Mathf.Lerp(Pitch, pitch, t));
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
