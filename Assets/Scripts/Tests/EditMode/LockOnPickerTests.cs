using System.Collections.Generic;
using Frieren.Player.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// Which target lock-on picks, and which one a flick moves to.
    /// </summary>
    /// <remarks>
    /// The reason the picker is plain functions over positions. "It grabbed the wrong one" is not
    /// reproducible by playing the game again; it is trivially reproducible by writing the three
    /// positions down.
    /// </remarks>
    public sealed class LockOnPickerTests
    {
        private static readonly Vector3 Eye = Vector3.zero;
        private static readonly Vector3 Forward = Vector3.forward;

        [Test]
        public void NothingToPickReturnsMinusOne()
        {
            Assert.AreEqual(-1, LockOnPicker.Best(new List<Vector3>(), Eye, Forward, 20f, 60f));
            Assert.AreEqual(-1, LockOnPicker.Best(null, Eye, Forward, 20f, 60f));
        }

        [Test]
        public void TheOneAheadWinsOverTheOneBehind()
        {
            var points = new List<Vector3>
            {
                new Vector3(0f, 0f, -3f),   // closer, but behind
                new Vector3(0f, 0f, 10f),   // further, but straight ahead
            };

            Assert.AreEqual(1, LockOnPicker.Best(points, Eye, Forward, 30f, 60f),
                "Nearest-first would grab the one behind the player, which reads as a broken button.");
        }

        [Test]
        public void TargetsBeyondTheRangeAreNotOffered()
        {
            var points = new List<Vector3> { new Vector3(0f, 0f, 40f) };

            Assert.AreEqual(-1, LockOnPicker.Best(points, Eye, Forward, 20f, 60f));
        }

        [Test]
        public void TargetsOutsideTheConeAreNotOffered()
        {
            // 80 degrees off forward, well inside the range.
            var points = new List<Vector3> { new Vector3(10f, 0f, 1.7f) };

            Assert.AreEqual(-1, LockOnPicker.Best(points, Eye, Forward, 30f, 45f));
        }

        [Test]
        public void DistanceBreaksATieBetweenEquallyAlignedTargets()
        {
            var points = new List<Vector3>
            {
                new Vector3(0f, 0f, 15f),
                new Vector3(0f, 0f, 6f),
            };

            Assert.AreEqual(1, LockOnPicker.Best(points, Eye, Forward, 30f, 60f));
        }

        [Test]
        public void AHeavyDistanceWeightPrefersTheNearerTargetOverTheStraighterOne()
        {
            var points = new List<Vector3>
            {
                new Vector3(0f, 0f, 20f),    // dead ahead, far
                new Vector3(4f, 0f, 5f),     // about 39 degrees off, near
            };

            Assert.AreEqual(0, LockOnPicker.Best(points, Eye, Forward, 30f, 60f, distanceWeight: 0.1f),
                "A light weight should follow the crosshair.");
            Assert.AreEqual(1, LockOnPicker.Best(points, Eye, Forward, 30f, 60f, distanceWeight: 5f),
                "A heavy weight should follow proximity.");
        }

        [Test]
        public void FlickingRightTakesTheNextTargetToTheRight()
        {
            var points = new List<Vector3>
            {
                new Vector3(-4f, 0f, 10f),
                new Vector3(0f, 0f, 10f),
                new Vector3(3f, 0f, 10f),
                new Vector3(9f, 0f, 10f),
            };

            int next = LockOnPicker.NextInDirection(points, 1, Eye, Vector3.right, 1f, 40f);
            Assert.AreEqual(2, next, "The nearest one to the right, not the furthest.");
        }

        [Test]
        public void FlickingLeftTakesTheNextTargetToTheLeft()
        {
            var points = new List<Vector3>
            {
                new Vector3(-9f, 0f, 10f),
                new Vector3(-3f, 0f, 10f),
                new Vector3(0f, 0f, 10f),
            };

            Assert.AreEqual(1, LockOnPicker.NextInDirection(points, 2, Eye, Vector3.right, -1f, 40f));
        }

        [Test]
        public void FlickingPastTheLastTargetStaysPut()
        {
            var points = new List<Vector3>
            {
                new Vector3(-3f, 0f, 10f),
                new Vector3(3f, 0f, 10f),
            };

            Assert.AreEqual(-1, LockOnPicker.NextInDirection(points, 1, Eye, Vector3.right, 1f, 40f),
                "Nothing further right means no switch, not a wrap round to the left-hand one.");
        }

        [Test]
        public void TargetsLevelWithTheCurrentOneAreNotASwitch()
        {
            // Two enemies at the same screen x. Without the guard these swap on every flick.
            var points = new List<Vector3>
            {
                new Vector3(2f, 0f, 10f),
                new Vector3(2f, 0f, 18f),
            };

            Assert.AreEqual(-1, LockOnPicker.NextInDirection(points, 0, Eye, Vector3.right, 1f, 40f));
        }

        [Test]
        public void SidesAreMeasuredAgainstTheCameraNotTheWorld()
        {
            var points = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 5f),
            };

            // Camera turned a quarter turn: its right axis now points down world -Z... or +Z,
            // depending which way it turned. Using world +Z as "right" must move to index 1.
            Assert.AreEqual(1, LockOnPicker.NextInDirection(points, 0, Eye, Vector3.forward, 1f, 40f));
            Assert.AreEqual(0, LockOnPicker.NextInDirection(points, 1, Eye, Vector3.forward, -1f, 40f));
        }

        [Test]
        public void AnOutOfRangeCurrentIndexIsRefusedRatherThanThrowing()
        {
            var points = new List<Vector3> { Vector3.forward };

            Assert.AreEqual(-1, LockOnPicker.NextInDirection(points, 5, Eye, Vector3.right, 1f, 40f));
            Assert.AreEqual(-1, LockOnPicker.NextInDirection(points, -1, Eye, Vector3.right, 1f, 40f));
        }
    }
}
