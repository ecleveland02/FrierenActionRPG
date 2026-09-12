using Frieren.Core.Input;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// The claim bookkeeping behind the pointer, which is the half that has bugs.
    /// </summary>
    /// <remarks>
    /// Whether <c>Cursor.lockState</c> ends up right is two assignments and visible the moment you
    /// press play. Whether closing one thing frees the pointer another thing is still holding is
    /// neither, and it is the failure that ships.
    /// </remarks>
    public sealed class CursorServiceTests
    {
        private CursorService cursor;
        private object wheel;
        private object menu;

        [SetUp]
        public void SetUp()
        {
            cursor = new CursorService();
            wheel = new object();
            menu = new object();
        }

        [TearDown]
        public void TearDown()
        {
            // Otherwise a test that captures the pointer leaves the editor's cursor locked.
            cursor.ReleaseAndShow();
        }

        [Test]
        public void GameplayIsTheStateWithNoClaims()
        {
            Assert.IsFalse(cursor.PointerWanted, "No claims must mean a captured pointer, not a free one.");
        }

        [Test]
        public void AClaimFreesThePointer()
        {
            Assert.IsTrue(cursor.RequestPointer(wheel));
            Assert.IsTrue(cursor.PointerWanted);
        }

        [Test]
        public void ReleasingTheLastClaimCapturesItAgain()
        {
            cursor.RequestPointer(wheel);
            cursor.ReleasePointer(wheel);

            Assert.IsFalse(cursor.PointerWanted);
        }

        [Test]
        public void OneOwnerReleasingDoesNotFreeAnother()
        {
            cursor.RequestPointer(wheel);
            cursor.RequestPointer(menu);

            cursor.ReleasePointer(wheel);

            Assert.IsTrue(cursor.PointerWanted,
                "The menu still wants the pointer; closing the wheel must not take it away.");
            Assert.IsTrue(cursor.HasClaim(menu));
            Assert.IsFalse(cursor.HasClaim(wheel));
        }

        [Test]
        public void ClaimingTwiceStillOnlyNeedsOneRelease()
        {
            cursor.RequestPointer(wheel);

            Assert.IsFalse(cursor.RequestPointer(wheel), "A second claim by the same owner is not a new claim.");
            Assert.AreEqual(1, cursor.ClaimCount);

            cursor.ReleasePointer(wheel);
            Assert.IsFalse(cursor.PointerWanted);
        }

        [Test]
        public void ReleasingAClaimNobodyFiledChangesNothing()
        {
            cursor.RequestPointer(wheel);

            Assert.IsFalse(cursor.ReleasePointer(menu));
            Assert.IsTrue(cursor.PointerWanted);
            Assert.AreEqual(1, cursor.ClaimCount);
        }

        [Test]
        public void ReleaseAllDropsEveryClaim()
        {
            cursor.RequestPointer(wheel);
            cursor.RequestPointer(menu);

            cursor.ReleaseAll();

            Assert.AreEqual(0, cursor.ClaimCount);
            Assert.IsFalse(cursor.PointerWanted, "Scene changes land back in the gameplay default.");
        }

        [Test]
        public void TheChangedEventFiresOnTransitionsAndNotOnEveryClaim()
        {
            int changes = 0;
            bool last = false;
            cursor.PointerWantedChanged += wanted => { changes++; last = wanted; };

            cursor.RequestPointer(wheel);
            cursor.RequestPointer(menu);

            Assert.AreEqual(1, changes, "The second claim did not change whether a pointer is wanted.");
            Assert.IsTrue(last);

            cursor.ReleasePointer(wheel);
            Assert.AreEqual(1, changes, "The menu still holds it, so nothing changed.");

            cursor.ReleasePointer(menu);
            Assert.AreEqual(2, changes);
            Assert.IsFalse(last);
        }

        [Test]
        public void ANullOwnerIsRejectedRatherThanSharedBetweenCallers()
        {
            Assert.IsFalse(cursor.RequestPointer(null));
            Assert.IsFalse(cursor.PointerWanted);
            Assert.IsFalse(cursor.ReleasePointer(null));
        }
    }
}
