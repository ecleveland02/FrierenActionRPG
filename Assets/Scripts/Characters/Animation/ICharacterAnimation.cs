namespace Frieren.Characters.Animation
{
    /// <summary>
    /// What gameplay tells the presentation layer. Implementations decide how - or whether - to show it.
    /// </summary>
    /// <remarks>
    /// This interface is the entire point of the "animation architecture" milestone item. Locomotion
    /// and dodge talk to it and never touch an <c>Animator</c>, so the placeholder capsule
    /// (<see cref="PlaceholderCharacterAnimation"/>) and the rigged Blender character arriving in
    /// Milestone 3 (<see cref="MecanimCharacterAnimation"/>) are interchangeable without a single
    /// change to gameplay code.
    ///
    /// It is one-directional on purpose: gameplay reports state, presentation reacts. Animation must
    /// never be the source of truth for whether the character is moving, because then a missing
    /// clip becomes a gameplay bug. Root motion, if it is ever wanted, gets added as an explicit
    /// opt-in on the Mecanim implementation rather than by inverting this.
    /// </remarks>
    public interface ICharacterAnimation
    {
        /// <summary>
        /// Continuous locomotion state.
        /// </summary>
        /// <param name="planarSpeed">Horizontal speed in metres per second.</param>
        /// <param name="normalizedSpeed">0 at rest, 1 at full sprint - the blend-tree input.</param>
        /// <param name="isGrounded">Whether the character is standing on something.</param>
        /// <param name="verticalVelocity">Signed vertical speed, for fall blending.</param>
        void SetLocomotion(float planarSpeed, float normalizedSpeed, bool isGrounded, float verticalVelocity);

        void PlayAction(CharacterAction action);
    }
}
