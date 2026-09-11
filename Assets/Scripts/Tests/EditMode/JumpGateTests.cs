using Frieren.Player;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class JumpGateTests
    {
        private const float Coyote = 0.1f;
        private const float Buffer = 0.15f;

        private JumpGate gate;

        [SetUp]
        public void SetUp() => gate = new JumpGate(Coyote, Buffer);

        [Test]
        public void GroundedPressJumps()
        {
            gate.NotifyGrounded(1f);
            gate.NotifyJumpPressed(1f);

            Assert.IsTrue(gate.TryConsume(1f));
        }

        [Test]
        public void NoPressMeansNoJump()
        {
            gate.NotifyGrounded(1f);

            Assert.IsFalse(gate.TryConsume(1f));
        }

        [Test]
        public void NeverGroundedMeansNoJump()
        {
            gate.NotifyJumpPressed(1f);

            Assert.IsFalse(gate.TryConsume(1f));
        }

        [Test]
        public void PressJustAfterLeavingGroundStillJumps()
        {
            gate.NotifyGrounded(1f);
            gate.NotifyJumpPressed(1f + Coyote * 0.5f);

            Assert.IsTrue(gate.TryConsume(1f + Coyote * 0.5f));
        }

        [Test]
        public void PressAfterCoyoteWindowExpiresDoesNotJump()
        {
            gate.NotifyGrounded(1f);
            gate.NotifyJumpPressed(1f + Coyote * 2f);

            Assert.IsFalse(gate.TryConsume(1f + Coyote * 2f));
        }

        [Test]
        public void PressBeforeLandingIsHonouredOnLanding()
        {
            gate.NotifyJumpPressed(1f);
            Assert.IsFalse(gate.TryConsume(1f), "Should not jump while still airborne.");

            gate.NotifyGrounded(1f + Buffer * 0.5f);

            Assert.IsTrue(gate.TryConsume(1f + Buffer * 0.5f));
        }

        [Test]
        public void PressTooLongBeforeLandingIsForgotten()
        {
            gate.NotifyJumpPressed(1f);
            gate.NotifyGrounded(1f + Buffer * 2f);

            Assert.IsFalse(gate.TryConsume(1f + Buffer * 2f));
        }

        [Test]
        public void OnePressCannotProduceTwoJumps()
        {
            gate.NotifyGrounded(1f);
            gate.NotifyJumpPressed(1f);

            Assert.IsTrue(gate.TryConsume(1f));
            Assert.IsFalse(gate.TryConsume(1f), "The same press must not be consumed twice.");
        }

        [Test]
        public void StillGroundedAfterJumpingNeedsAFreshPress()
        {
            gate.NotifyGrounded(1f);
            gate.NotifyJumpPressed(1f);
            gate.TryConsume(1f);

            gate.NotifyGrounded(1.05f);
            Assert.IsFalse(gate.TryConsume(1.05f));

            gate.NotifyJumpPressed(1.05f);
            Assert.IsTrue(gate.TryConsume(1.05f));
        }

        [Test]
        public void ResetDropsBothWindows()
        {
            gate.NotifyGrounded(1f);
            gate.NotifyJumpPressed(1f);
            gate.Reset();

            Assert.IsFalse(gate.TryConsume(1f));
        }

        [Test]
        public void NegativeWindowsAreClampedToZero()
        {
            var strict = new JumpGate(-5f, -5f);
            strict.NotifyGrounded(1f);
            strict.NotifyJumpPressed(1f);

            Assert.IsTrue(strict.TryConsume(1f), "A zero window must still allow a same-instant press.");

            strict.NotifyGrounded(2f);
            strict.NotifyJumpPressed(2f);
            Assert.IsFalse(strict.TryConsume(2.01f), "A zero window must expire immediately.");
        }
    }
}
