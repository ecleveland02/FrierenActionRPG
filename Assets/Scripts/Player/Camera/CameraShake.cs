using Frieren.Core.Services;
using Frieren.Core.Timing;
using UnityEngine;

namespace Frieren.Player.Cameras
{
    /// <summary>
    /// Kicks the camera on impact, and answers <see cref="IScreenShake"/> for anything that asks.
    /// </summary>
    /// <remarks>
    /// Sits beside <see cref="OrbitCameraRig"/> and offsets the position the rig computed, in
    /// <c>LateUpdate</c> at a later execution order. Folding the shake into the rig's own solve
    /// would put a presentation effect inside the component that decides where the camera belongs,
    /// and every future camera behaviour would have to be written around it.
    ///
    /// Decays on unscaled time. A shake triggered by a hit that also dipped the time scale would
    /// otherwise last as long as the dip stretched it, which is precisely backwards - the impact
    /// should feel sharper, not longer.
    ///
    /// It registers itself as the screen-shake service, so gameplay never learns there is a camera.
    /// </remarks>
    [RequireComponent(typeof(OrbitCameraRig))]
    [DefaultExecutionOrder(ShakeExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour, IScreenShake
    {
        /// <summary>After the rig, which runs at the default order, so it offsets a settled position.</summary>
        public const int ShakeExecutionOrder = 200;

        [SerializeField]
        [Tooltip("Cap on displacement in metres, so a pile-up of hits cannot throw the camera across the room.")]
        private float maximumStrength = 0.45f;

        [SerializeField]
        [Tooltip("Shakes per second. Higher is a buzz, lower is a lurch.")]
        private float frequency = 26f;

        private float strength;
        private float decayPerSecond;
        private Vector2 seed;

        private void Awake() => seed = new Vector2(Random.value * 100f, Random.value * 100f);

        private void OnEnable() => ServiceLocator.Register<IScreenShake>(this);

        private void OnDisable()
        {
            if (ServiceLocator.TryGet(out IScreenShake registered) && ReferenceEquals(registered, this))
            {
                ServiceLocator.Unregister<IScreenShake>();
            }
        }

        public void Shake(float newStrength, float seconds)
        {
            if (newStrength <= 0f || seconds <= 0f)
            {
                return;
            }

            // A new shake takes over only if it is harder than what is already running. Otherwise a
            // stream of small hits would keep resetting a big one down to nothing.
            newStrength = Mathf.Min(newStrength, maximumStrength);

            if (newStrength < strength)
            {
                return;
            }

            strength = newStrength;
            decayPerSecond = newStrength / seconds;
        }

        private void LateUpdate()
        {
            if (strength <= 0f)
            {
                return;
            }

            float time = Time.unscaledTime * frequency;

            // Perlin rather than Random: successive frames are related, so the camera swings rather
            // than jitters. Two channels sampled far apart keep the axes independent.
            var offset = new Vector3(
                (Mathf.PerlinNoise(seed.x + time, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, seed.y + time) - 0.5f) * 2f,
                0f) * strength;

            transform.position += transform.rotation * offset;

            strength = Mathf.Max(0f, strength - decayPerSecond * Time.unscaledDeltaTime);
        }
    }
}
