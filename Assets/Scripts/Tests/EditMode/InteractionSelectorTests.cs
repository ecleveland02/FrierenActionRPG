using System.Collections.Generic;
using Frieren.Player;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    public sealed class InteractionSelectorTests
    {
        private const float MaxDistance = 3f;
        private const float MaxAngle = 90f;

        private static int Select(IReadOnlyList<Vector3> positions, float angleWeight = 1.5f)
        {
            return InteractionSelector.SelectBestIndex(
                positions, Vector3.zero, Vector3.forward, MaxDistance, MaxAngle, angleWeight);
        }

        [Test]
        public void NoCandidatesSelectsNothing()
        {
            Assert.AreEqual(-1, Select(new List<Vector3>()));
            Assert.AreEqual(-1, InteractionSelector.SelectBestIndex(
                null, Vector3.zero, Vector3.forward, MaxDistance, MaxAngle));
        }

        [Test]
        public void CandidateBeyondRangeIsRejected()
        {
            Assert.AreEqual(-1, Select(new List<Vector3> { new Vector3(0f, 0f, MaxDistance + 1f) }));
        }

        [Test]
        public void CandidateBehindThePlayerIsRejected()
        {
            Assert.AreEqual(-1, Select(new List<Vector3> { new Vector3(0f, 0f, -1f) }));
        }

        [Test]
        public void HeightAloneDoesNotRejectACandidate()
        {
            // An interaction point on a tall object sits well above the probe origin; that must not
            // read as "off to the side".
            int index = Select(new List<Vector3> { new Vector3(0f, 1.5f, 1f) });

            Assert.AreEqual(0, index);
        }

        [Test]
        public void FacedCandidateBeatsASlightlyNearerOneOffToTheSide()
        {
            var positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 1.2f),   // dead ahead
                new Vector3(1.0f, 0f, 0.1f), // nearer to being beside the player
            };

            Assert.AreEqual(0, Select(positions));
        }

        [Test]
        public void AmongEquallyFacedCandidatesTheNearerWins()
        {
            var positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 2.5f),
                new Vector3(0f, 0f, 1f),
            };

            Assert.AreEqual(1, Select(positions));
        }

        [Test]
        public void RaisingTheAngleWeightFavoursWhatThePlayerFaces()
        {
            var positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 2.4f),   // far, dead ahead
                new Vector3(0.55f, 0f, 0.5f) // close, off to the side
            };

            Assert.AreEqual(1, Select(positions, angleWeight: 0.1f), "Low weight should favour proximity.");
            Assert.AreEqual(0, Select(positions, angleWeight: 6f), "High weight should favour aim.");
        }

        [Test]
        public void CandidateAtTheOriginIsAcceptedRatherThanDiscarded()
        {
            // Standing inside the trigger volume leaves no meaningful direction to score.
            Assert.AreEqual(0, Select(new List<Vector3> { Vector3.zero }));
        }

        [Test]
        public void UndefinedFacingSelectsNothing()
        {
            int index = InteractionSelector.SelectBestIndex(
                new List<Vector3> { Vector3.forward }, Vector3.zero, Vector3.up, MaxDistance, MaxAngle);

            Assert.AreEqual(-1, index, "A purely vertical facing has no ground-plane direction to compare against.");
        }

        [Test]
        public void NonPositiveLimitsSelectNothing()
        {
            var positions = new List<Vector3> { Vector3.forward };

            Assert.AreEqual(-1, InteractionSelector.SelectBestIndex(
                positions, Vector3.zero, Vector3.forward, 0f, MaxAngle));
            Assert.AreEqual(-1, InteractionSelector.SelectBestIndex(
                positions, Vector3.zero, Vector3.forward, MaxDistance, 0f));
        }
    }
}
