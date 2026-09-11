using System.Collections.Generic;
using Frieren.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    public sealed class CharacterActionLockTests
    {
        private GameObject host;
        private CharacterActionLock actionLock;

        private readonly object dodge = new object();
        private readonly object cast = new object();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("LockHost");
            actionLock = host.AddComponent<CharacterActionLock>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void StartsUnlocked()
        {
            Assert.IsFalse(actionLock.IsLocked);
        }

        [Test]
        public void FirstClaimantAcquiresTheLock()
        {
            Assert.IsTrue(actionLock.TryAcquire(dodge));
            Assert.IsTrue(actionLock.IsLocked);
            Assert.IsTrue(actionLock.IsHeldBy(dodge));
        }

        [Test]
        public void SecondClaimantIsRefused()
        {
            actionLock.TryAcquire(dodge);

            Assert.IsFalse(actionLock.TryAcquire(cast));
            Assert.IsTrue(actionLock.IsHeldBy(dodge), "The original holder must keep the lock.");
        }

        [Test]
        public void ReacquiringByTheSameHolderSucceedsWithoutReraising()
        {
            int acquiredCount = 0;
            actionLock.Acquired += _ => acquiredCount++;

            Assert.IsTrue(actionLock.TryAcquire(dodge));
            Assert.IsTrue(actionLock.TryAcquire(dodge));

            Assert.AreEqual(1, acquiredCount, "Re-entrant acquire must not raise Acquired twice.");
        }

        [Test]
        public void ReleaseFreesTheLock()
        {
            actionLock.TryAcquire(dodge);

            Assert.IsTrue(actionLock.Release(dodge));
            Assert.IsFalse(actionLock.IsLocked);
            Assert.IsTrue(actionLock.TryAcquire(cast), "A freed lock must be claimable by anyone.");
        }

        [Test]
        public void ANonHolderCannotReleaseTheLock()
        {
            actionLock.TryAcquire(dodge);

            Assert.IsFalse(actionLock.Release(cast));
            Assert.IsTrue(actionLock.IsHeldBy(dodge));
        }

        [Test]
        public void ReleasingAnUnlockedLockIsHarmless()
        {
            Assert.IsFalse(actionLock.Release(dodge));
            Assert.IsFalse(actionLock.IsLocked);
        }

        [Test]
        public void ForceReleaseDropsTheLockRegardlessOfHolder()
        {
            actionLock.TryAcquire(dodge);

            actionLock.ForceRelease();

            Assert.IsFalse(actionLock.IsLocked);
            Assert.IsFalse(actionLock.IsHeldBy(dodge));
        }

        [Test]
        public void ForceReleaseOnAnUnlockedLockRaisesNothing()
        {
            int releasedCount = 0;
            actionLock.Released += _ => releasedCount++;

            actionLock.ForceRelease();

            Assert.AreEqual(0, releasedCount);
        }

        [Test]
        public void EventsReportTheHolder()
        {
            var log = new List<string>();
            actionLock.Acquired += owner => log.Add($"acquired:{owner == dodge}");
            actionLock.Released += owner => log.Add($"released:{owner == dodge}");

            actionLock.TryAcquire(dodge);
            actionLock.Release(dodge);

            CollectionAssert.AreEqual(new[] { "acquired:True", "released:True" }, log);
        }

        [Test]
        public void NullClaimantIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => actionLock.TryAcquire(null));
            Assert.IsFalse(actionLock.IsHeldBy(null));
        }
    }
}
