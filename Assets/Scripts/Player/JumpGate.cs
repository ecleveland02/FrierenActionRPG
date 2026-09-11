namespace Frieren.Player
{
    /// <summary>
    /// Decides whether a jump is allowed, applying coyote time and input buffering.
    /// </summary>
    /// <remarks>
    /// Both forgiveness windows exist because a jump that is strictly "grounded AND pressed this
    /// frame" feels broken at 60fps even when it is technically correct: players press jump a frame
    /// or two before landing, and they walk off ledges a frame before pressing. Keeping the rule in
    /// a plain class rather than inline in the controller is what makes it verifiable - the
    /// behaviour is entirely about timing, which is miserable to confirm by feel and trivial to
    /// confirm in a test.
    ///
    /// Times are passed in rather than read from <c>Time.time</c> so tests can drive the clock.
    /// </remarks>
    public sealed class JumpGate
    {
        private readonly float coyoteTime;
        private readonly float bufferTime;

        private float lastGroundedTime = float.NegativeInfinity;
        private float lastPressTime = float.NegativeInfinity;

        /// <param name="coyoteTime">How long after leaving the ground a jump is still allowed.</param>
        /// <param name="bufferTime">How long before landing a jump press stays queued.</param>
        public JumpGate(float coyoteTime, float bufferTime)
        {
            this.coyoteTime = coyoteTime < 0f ? 0f : coyoteTime;
            this.bufferTime = bufferTime < 0f ? 0f : bufferTime;
        }

        /// <summary>Call every frame the character is grounded.</summary>
        public void NotifyGrounded(float time) => lastGroundedTime = time;

        /// <summary>Call when the jump input fires.</summary>
        public void NotifyJumpPressed(float time) => lastPressTime = time;

        public bool HasBufferedPress(float time) => time - lastPressTime <= bufferTime;

        public bool HasCoyoteTime(float time) => time - lastGroundedTime <= coyoteTime;

        /// <summary>
        /// Consumes a queued jump if one is allowed. Both windows are cleared on success, so a
        /// single press can never produce two jumps.
        /// </summary>
        public bool TryConsume(float time)
        {
            if (!HasBufferedPress(time) || !HasCoyoteTime(time))
            {
                return false;
            }

            lastPressTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            return true;
        }

        /// <summary>Drops both windows, e.g. when control is taken away.</summary>
        public void Reset()
        {
            lastPressTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
        }
    }
}
