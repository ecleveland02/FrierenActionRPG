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

        [Test]
        public void AZeroFramingOffsetLeavesThePivotAlone()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(40f, 20f);

            Vector3 pivot = new Vector3(2f, 1f, 3f);

            Assert.AreEqual(pivot, solver.FramedPivot(pivot, Vector2.zero));
        }

        [Test]
        public void APositiveFramingOffsetLiftsThePivotSoTheSubjectSitsLow()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(0f, 0f);

            Vector3 framed = solver.FramedPivot(Vector3.zero, new Vector2(0f, 0.5f));

            Assert.AreEqual(0.5f, framed.y, 0.001f,
                "Looking level, camera-up is world-up, so the pivot rises by the whole offset.");
            Assert.AreEqual(0f, framed.x, 0.001f);
            Assert.AreEqual(0f, framed.z, 0.001f);
        }

        [Test]
        public void TheFramingOffsetIsInCameraSpaceNotWorldSpace()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(90f, 0f);

            // Yawed a quarter turn, camera-right points down world -Z.
            Vector3 framed = solver.FramedPivot(Vector3.zero, new Vector2(1f, 0f));

            Assert.AreEqual(0f, framed.x, 0.001f);
            Assert.AreEqual(-1f, framed.z, 0.001f);
        }

        [Test]
        public void FramingDoesNotChangeHowFarTheCameraSits()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(35f, 15f);

            Vector3 pivot = new Vector3(1f, 2f, 3f);
            Vector3 framed = solver.FramedPivot(pivot, new Vector2(0.2f, 0.55f));

            Assert.AreEqual(5f, Vector3.Distance(framed, solver.DesiredPosition(framed, 5f)), 0.001f,
                "The offset moves the whole rig, so the boom length is untouched.");
        }

        [Test]
        public void LookAnglesFaceTheTarget()
        {
            Vector2 angles = OrbitCameraSolver.LookAngles(Vector3.zero, new Vector3(0f, 0f, 10f));
            Assert.AreEqual(0f, angles.x, 0.001f, "Straight along +Z is zero yaw.");
            Assert.AreEqual(0f, angles.y, 0.001f);

            angles = OrbitCameraSolver.LookAngles(Vector3.zero, new Vector3(10f, 0f, 0f));
            Assert.AreEqual(90f, angles.x, 0.001f, "Along +X is a quarter turn clockwise from north.");
        }

        [Test]
        public void LookAnglesPitchDownForSomethingBelow()
        {
            Vector2 up = OrbitCameraSolver.LookAngles(Vector3.zero, new Vector3(0f, 10f, 10f));
            Vector2 down = OrbitCameraSolver.LookAngles(Vector3.zero, new Vector3(0f, -10f, 10f));

            Assert.AreEqual(-45f, up.y, 0.001f, "Positive pitch looks down, so looking up is negative.");
            Assert.AreEqual(45f, down.y, 0.001f);
        }

        [Test]
        public void LookAnglesDoNotDivideByZeroStraightUp()
        {
            Vector2 angles = OrbitCameraSolver.LookAngles(Vector3.zero, new Vector3(0f, 5f, 0f));

            Assert.IsFalse(float.IsNaN(angles.x));
            Assert.IsFalse(float.IsNaN(angles.y));
            Assert.Less(angles.y, 0f, "Straight up is still up.");
        }

        [Test]
        public void BlendTowardsTakesTheShortWayRoundTheWrap()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(170f, 0f);

            solver.BlendTowards(-170f, 0f, 0.5f);

            // Halfway across the 20 degree gap through 180, not halfway across the 340 the other way.
            Assert.AreEqual(180f, Mathf.Abs(solver.Yaw), 0.001f);
        }

        [Test]
        public void BlendTowardsRespectsThePitchLimits()
        {
            var solver = new OrbitCameraSolver(-30f, 60f);
            solver.SetOrientation(0f, 0f);

            solver.BlendTowards(0f, 400f, 1f);

            Assert.AreEqual(60f, solver.Pitch, 0.001f,
                "A lock target overhead must not tip the camera past its own limits.");
        }

        [Test]
        public void BlendTowardsWithZeroDoesNothingAndWithOneArrives()
        {
            var solver = new OrbitCameraSolver();
            solver.SetOrientation(10f, 5f);

            solver.BlendTowards(90f, 30f, 0f);
            Assert.AreEqual(10f, solver.Yaw, 0.001f);

            solver.BlendTowards(90f, 30f, 1f);
            Assert.AreEqual(90f, solver.Yaw, 0.001f);
            Assert.AreEqual(30f, solver.Pitch, 0.001f);
        }
    }
}
