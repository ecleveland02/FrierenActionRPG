namespace Frieren.Core.Timing
{
    /// <summary>
    /// Something that can kick the view on impact. Implemented by the camera, requested by anything.
    /// </summary>
    /// <remarks>
    /// An interface in Core rather than a direct call to the camera rig, for the same reason as
    /// <c>IMagicReceiver</c>: the things that want to shake the screen live in gameplay assemblies
    /// that must not depend on the player's camera, and the camera must not know what a sword is.
    ///
    /// Every caller treats it as optional. If nothing is registered, requests go nowhere and the
    /// game plays exactly as before, which is what a presentation layer should do when absent.
    /// </remarks>
    public interface IScreenShake
    {
        /// <summary>
        /// Kicks the view.
        /// </summary>
        /// <param name="strength">Displacement in metres. 0.1 is a nudge, 0.4 is a heavy hit.</param>
        /// <param name="seconds">How long it decays over, in real seconds.</param>
        void Shake(float strength, float seconds);
    }
}
