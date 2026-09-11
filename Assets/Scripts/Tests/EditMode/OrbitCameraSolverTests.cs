using Frieren.Player.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    public sealed class OrbitCameraSolverTests
    {
        [Test]
        public void PitchIsClampedAtBothLimits()
        {
            var solver = new OrbitCameraSolver(-30f, 65f);

            solver.ApplyLook(new Vector2(0f, -1000f));
            Assert.AreEqual(65f, solver.Pitch, 0.001f, "Looking down must stop at the lower limit.");

            solver.ApplyLook(new Vector2(0f, 1000f));
            Assert.AreEqual(-30f, solver.Pitch, 0.001f, "Looking up must stop at the upper limit.");
        }

        [Test]
        public void PushingUpLooksUp()
        {
            var solver = new OrbitCameraSolver(-80f, 80f);

            solver.ApplyLook(new Vector2(0f, 10f));

            Assert.Less(solver.Pitch, 0f, "Positive look Y should raise the view, which is negative pitch.");
        }

        [Test]
        public void YawStaysBoundedAcrossManyRevolutions()
        {
            var solver = new OrbitCameraSolver();

            for (int i = 0; i < 500; i++)
            {
                solver.ApplyLook(new Vector2(37f, 0f));
            }

            Assert.LessOrEqual(Mathf.Abs(solver.Yaw), 180f, "Yaw must not accumulate without bound.");
        }

        [TestCase(0f, 0f)]
        [TestCase(90f, 90f)]
        [TestCase(180f, 180f)]
        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(370f, 10f)]
        [TestCase(-370f, -10f)]
        [TestCase(-180f, 180f)]
        public void WrapAngleNormalisesToHalfOpenRange(float input, float expected)
        {
            Assert.AreEqual(expected, OrbitCameraSolver.WrapAngle(input), 0.001f);
        }

        [Test]
        public void SwappedPitchLimitsAreCorrectedRatherThanBreaking()
        {
            var solver = new OrbitCameraSolver(70f, -20f);

            Assert.AreEqual(-20f, solver.MinPitch, 0.001f);
            Assert.AreEqual(70f, solver.MaxPitch, 0.001f);
        }

        [Test]
        public void TighteningLimitsPullsCurrentPitchIntoRange()
        {
            var solver = new OrbitCameraSolver(-80f, 80f);
            solver.SetOrientation(0f, 75f);

            solver.SetPitchLimits(-10f, 20f);

            Assert.AreEqual(20f, solver.Pitch, 0.001f);
        }

        [Test]
        public void CameraSitsBehindThePivotAtRest()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(0f, 0f);

            Vector3 position = solver.DesiredPosition(Vector3.zero, 5f);

            Assert.AreEqual(0f, position.x, 0.001f);
            Assert.AreEqual(0f, position.y, 0.001f);
            Assert.AreEqual(-5f, position.z, 0.001f);
        }

        [Test]
        public void CameraOrbitsWithYaw()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(90f, 0f);

            Vector3 position = solver.DesiredPosition(Vector3.zero, 5f);

            Assert.AreEqual(-5f, position.x, 0.001f);
            Assert.AreEqual(0f, position.z, 0.001f);
        }

        [Test]
        public void LookingDownRaisesTheCamera()
        {
            var solver = new OrbitCameraSolver(-80f, 80f);
            solver.SetOrientation(0f, 45f);

            Vector3 position = solver.DesiredPosition(Vector3.zero, 5f);

            Assert.Greater(position.y, 0f, "Positive pitch looks down, so the camera must be above the pivot.");
        }

        [Test]
        public void DesiredPositionIsAlwaysTheRequestedDistanceAway()
        {
            var solver = new OrbitCameraSolver(-80f, 80f);
            solver.SetOrientation(123f, 40f);

            Vector3 pivot = new Vector3(3f, 1f, -7f);

            Assert.AreEqual(5f, Vector3.Distance(pivot, solver.DesiredPosition(pivot, 5f)), 0.001f);
        }
    }
}
