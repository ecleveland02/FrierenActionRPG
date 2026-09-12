using Frieren.Player;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// Which slot a direction points at. Static and frame-free, so it belongs here rather than in a
    /// play-mode test - and getting it wrong is the kind of bug that reads as "the wheel feels
    /// off" rather than as anything you could point at.
    /// </summary>
    public sealed class SpellWheelGeometryTests
    {
        private const int Slots = 8;

        [Test]
        public void StraightUpIsTheFirstSlot()
        {
            Assert.AreEqual(0, SpellWheelInput.SlotFor(Vector2.up, Slots));
        }

        [Test]
        public void SlotsRunClockwiseFromTheTop()
        {
            Assert.AreEqual(2, SpellWheelInput.SlotFor(Vector2.right, Slots), "A quarter turn clockwise of eight.");
            Assert.AreEqual(4, SpellWheelInput.SlotFor(Vector2.down, Slots));
            Assert.AreEqual(6, SpellWheelInput.SlotFor(Vector2.left, Slots));
        }

        [Test]
        public void EachSlotClaimsItsOwnCentre()
        {
            for (int i = 0; i < Slots; i++)
            {
                Vector2 centre = SpellWheelInput.SlotOffset(i, Slots, 1f);

                Assert.AreEqual(i, SpellWheelInput.SlotFor(centre, Slots),
                    $"Slot {i} must claim the direction it is drawn at, or the wheel lies about itself.");
            }
        }

        [Test]
        public void ASlotClaimsJustInsideItsOwnEdges()
        {
            // Slot 1 of eight is centred at 45 degrees and owns 22.5 to 67.5.
            Assert.AreEqual(1, SpellWheelInput.SlotFor(Direction(23f), Slots));
            Assert.AreEqual(1, SpellWheelInput.SlotFor(Direction(67f), Slots));
            Assert.AreNotEqual(1, SpellWheelInput.SlotFor(Direction(22f), Slots));
            Assert.AreNotEqual(1, SpellWheelInput.SlotFor(Direction(68f), Slots));
        }

        [Test]
        public void TheWheelWrapsAroundZero()
        {
            Assert.AreEqual(0, SpellWheelInput.SlotFor(Direction(355f), Slots), "Just short of straight up.");
            Assert.AreEqual(0, SpellWheelInput.SlotFor(Direction(5f), Slots));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(9)]
        public void EverySlotCountCoversTheWholeCircleExactlyOnce(int slotCount)
        {
            var seen = new bool[slotCount];

            for (int degrees = 0; degrees < 360; degrees++)
            {
                int slot = SpellWheelInput.SlotFor(Direction(degrees), slotCount);

                Assert.GreaterOrEqual(slot, 0, $"{degrees} degrees fell outside the wheel.");
                Assert.Less(slot, slotCount, $"{degrees} degrees selected a slot that does not exist.");
                seen[slot] = true;
            }

            for (int i = 0; i < slotCount; i++)
            {
                Assert.IsTrue(seen[i], $"Slot {i} of {slotCount} is unreachable at any angle.");
            }
        }

        [Test]
        public void AZeroDirectionSelectsNothing()
        {
            Assert.AreEqual(-1, SpellWheelInput.SlotFor(Vector2.zero, Slots),
                "The dead zone in the middle must not resolve to a slot.");
        }

        [Test]
        public void AnEmptyWheelSelectsNothing()
        {
            Assert.AreEqual(-1, SpellWheelInput.SlotFor(Vector2.up, 0));
            Assert.AreEqual(Vector2.zero, SpellWheelInput.SlotOffset(0, 0, 100f));
        }

        [Test]
        public void SlotOffsetsSitOnTheRadius()
        {
            for (int i = 0; i < Slots; i++)
            {
                Assert.AreEqual(150f, SpellWheelInput.SlotOffset(i, Slots, 150f).magnitude, 0.01f);
            }
        }

        private static Vector2 Direction(float clockwiseDegreesFromUp)
        {
            float radians = clockwiseDegreesFromUp * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }
    }
}
