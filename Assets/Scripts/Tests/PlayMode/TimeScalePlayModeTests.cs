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
