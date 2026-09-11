using Frieren.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    public sealed class MotorMathTests
    {
        [Test]
        public void JumpSpeedReachesTheRequestedHeight()
        {
            const float gravity = 24f;
            const float height = 1.6f;

            float speed = MotorMath.JumpSpeedForHeight(height, gravity);

            // Projectile motion: peak = v^2 / 2g.
            Assert.AreEqual(height, speed * speed / (2f * gravity), 0.001f);
        }

        [Test]
        public void JumpSpeedIsZeroForNonPositiveInputs()
        {
            Assert.AreEqual(0f, MotorMath.JumpSpeedForHeight(0f, 24f));
            Assert.AreEqual(0f, MotorMath.JumpSpeedForHeight(-1f, 24f));
            Assert.AreEqual(0f, MotorMath.JumpSpeedForHeight(1f, 0f));
        }

        [Test]
        public void GravityAccumulatesDownward()
        {
            float v = MotorMath.ApplyGravity(0f, 10f, 100f, 0.5f);

            Assert.AreEqual(-5f, v, 0.0001f);
        }

        [Test]
        public void FallSpeedIsCappedAtTerminalVelocity()
        {
            float v = 0f;

            for (int i = 0; i < 1000; i++)
            {
                v = MotorMath.ApplyGravity(v, 24f, 45f, 0.016f);
            }

            Assert.AreEqual(-45f, v, 0.0001f);
        }

        [Test]
        public void TerminalSpeedSignIsIgnored()
        {
            float fromNegative = MotorMath.ApplyGravity(-100f, 24f, -45f, 0.016f);

            Assert.AreEqual(-45f, fromNegative, 0.0001f);
        }

        [Test]
        public void MoveTowardsRateIsFrameRateIndependent()
        {
            Vector3 target = new Vector3(10f, 0f, 0f);

            Vector3 coarse = MotorMath.MoveTowardsRate(Vector3.zero, target, 20f, 0.1f);

            Vector3 fine = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                fine = MotorMath.MoveTowardsRate(fine, target, 20f, 0.01f);
            }

            Assert.AreEqual(coarse.x, fine.x, 0.0001f);
        }

        [Test]
        public void CameraRelativeDirectionForwardMatchesCameraYaw()
        {
            Quaternion yawed = Quaternion.Euler(0f, 90f, 0f);

            Vector3 direction = MotorMath.CameraRelativeDirection(Vector2.up, yawed);

            Assert.AreEqual(1f, direction.x, 0.0001f);
            Assert.AreEqual(0f, direction.z, 0.0001f);
        }

        [Test]
        public void CameraPitchDoesNotLeakIntoMovement()
        {
            Quaternion pitched = Quaternion.Euler(60f, 0f, 0f);

            Vector3 direction = MotorMath.CameraRelativeDirection(Vector2.up, pitched);

            Assert.AreEqual(0f, direction.y, 0.0001f, "Movement must stay on the ground plane.");
            Assert.AreEqual(1f, direction.magnitude, 0.0001f, "Pitch must not shorten the direction.");
        }

        [Test]
        public void DiagonalInputIsNotFasterThanCardinal()
        {
            Vector3 diagonal = MotorMath.CameraRelativeDirection(new Vector2(1f, 1f), Quaternion.identity);

            Assert.AreEqual(1f, diagonal.magnitude, 0.0001f);
        }

        [Test]
        public void PartialStickDeflectionIsPreserved()
        {
            Vector3 half = MotorMath.CameraRelativeDirection(new Vector2(0f, 0.5f), Quaternion.identity);

            Assert.AreEqual(0.5f, half.magnitude, 0.0001f, "Walking slowly must remain possible on a stick.");
        }

        [Test]
        public void ZeroInputGivesZeroDirection()
        {
            Assert.AreEqual(Vector3.zero, MotorMath.CameraRelativeDirection(Vector2.zero, Quaternion.identity));
        }

        [Test]
        public void StraightDownCameraStillProducesMovement()
        {
            Quaternion topDown = Quaternion.Euler(90f, 0f, 0f);

            Vector3 direction = MotorMath.CameraRelativeDirection(Vector2.up, topDown);

            Assert.Greater(direction.magnitude, 0.9f, "A top-down camera must not freeze movement.");
            Assert.AreEqual(0f, direction.y, 0.0001f);
        }
    }
}
