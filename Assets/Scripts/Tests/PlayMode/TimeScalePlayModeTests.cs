using System.Collections;
using Frieren.Core.Services;
using Frieren.Core.Timing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// The time service against the real clock. The arithmetic is covered in edit mode; this checks
    /// that it is wired to a clock that actually moves and that it writes the real time scale.
    /// </summary>
    public sealed class TimeScalePlayModeTests : PlayModeTestBase
    {
        private TimeScaleService service;

        [SetUp]
        public void CreateService()
        {
            service = new TimeScaleService();
            ServiceLocator.Register(service);
        }

        [UnityTest]
        public IEnumerator ADipSlowsTheRealTimeScaleAndRecovers()
        {
            service.RequestDip(0.1f, 0.2f);

            Assert.AreEqual(0.1f, Time.timeScale, 0.001f);

            yield return WaitForRealtime(0.35f);
            service.Tick();

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [UnityTest]
        public IEnumerator ADipTimedInRealSecondsIsNotStretchedByItself()
        {
            float start = Time.realtimeSinceStartup;
            service.RequestDip(0.05f, 0.2f);

            while (service.IsDipping)
            {
                service.Tick();

                if (Time.realtimeSinceStartup - start > 1.5f)
                {
                    Assert.Fail("A dip measured in scaled seconds would slow its own expiry. " +
                                "It must use the unscaled clock.");
                }

                yield return null;
            }

            Assert.Less(Time.realtimeSinceStartup - start, 1f);
        }

        [UnityTest]
        public IEnumerator PausingDuringADipStopsTimeAndUnpausingRestoresIt()
        {
            service.RequestDip(0.1f, 5f);
            service.ClearDip();
            service.BaseScale = 0f;

            Assert.AreEqual(0f, Time.timeScale, 0.001f);

            yield return WaitForRealtime(0.15f);
            service.Tick();

            Assert.AreEqual(0f, Time.timeScale, 0.001f, "Ticking must not unpause the game.");

            service.BaseScale = 1f;

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [UnityTest]
        public IEnumerator ADipExpiringWhilePausedLeavesTheGamePaused()
        {
            // The exact sequence the split exists to make impossible: a hit lands, then a pause
            // arrives before the dip has finished.
            service.RequestDip(0.1f, 0.1f);
            service.BaseScale = 0f;

            yield return WaitForRealtime(0.3f);
            service.Tick();

            Assert.AreEqual(0f, Time.timeScale, 0.001f);

            service.BaseScale = 1f;

            Assert.AreEqual(1f, Time.timeScale, 0.001f,
                "And unpausing must return to full speed, not to the dipped speed.");
        }

        [UnityTest]
        public IEnumerator AHoldSlowsTheRealTimeScaleUntilItsOwnerReleases()
        {
            var owner = new object();

            Assert.IsTrue(service.TryHold(owner, 0.25f));
            Assert.AreEqual(0.25f, Time.timeScale, 0.001f);

            yield return WaitForRealtime(0.2f);
            service.Tick();

            Assert.AreEqual(0.25f, Time.timeScale, 0.001f, "A hold does not expire on its own.");

            Assert.IsTrue(service.ReleaseHold(owner));
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [UnityTest]
        public IEnumerator OnlyTheOwnerCanReleaseAHold()
        {
            var owner = new object();
            var someoneElse = new object();

            service.TryHold(owner, 0.3f);

            Assert.IsFalse(service.ReleaseHold(someoneElse));
            Assert.AreEqual(0.3f, Time.timeScale, 0.001f,
                "Anyone being able to cancel a hold is how time ends up stuck at a third speed.");

            Assert.IsFalse(service.TryHold(someoneElse, 0.9f), "A second claimant is refused.");
            Assert.AreEqual(0.3f, Time.timeScale, 0.001f);

            service.ReleaseHold(owner);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForceClearHoldRecoversFromALostOwner()
        {
            service.TryHold(new object(), 0.2f);
            service.ForceClearHold();

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
            Assert.IsTrue(service.TryHold(new object(), 0.5f), "The claim is free again.");

            yield return null;
        }

        /// <summary>Waits on the unscaled clock, since these tests deliberately slow the scaled one.</summary>
        private static IEnumerator WaitForRealtime(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }
    }
}
