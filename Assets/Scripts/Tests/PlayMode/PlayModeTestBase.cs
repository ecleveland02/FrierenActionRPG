using System;
using System.Collections;
using Frieren.Core.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Shared setup for play-mode tests: a clean world, a clean service registry, a normal clock.
    /// </summary>
    /// <remarks>
    /// Play-mode tests share one scene and one process. Anything a test leaves behind - a collider,
    /// a registered service, a time scale of 0.1 from a hit-stop - is the next test's problem, and
    /// the symptom is a failure in a test that has nothing to do with the cause. All three are
    /// therefore reset around every test rather than at the end of the run.
    /// </remarks>
    public abstract class PlayModeTestBase
    {
        protected TestWorld World { get; private set; }

        [SetUp]
        public void BaseSetUp()
        {
            Time.timeScale = 1f;
            ServiceLocator.Clear();
            World = new TestWorld();
            World.CreateGround();
        }

        [UnityTearDown]
        public IEnumerator BaseTearDown()
        {
            World.Dispose();
            ServiceLocator.Clear();
            Time.timeScale = 1f;

            // Destroy is deferred to the end of the frame, so without this the next test starts with
            // the previous test's colliders still in the physics scene.
            yield return null;
        }

        /// <summary>
        /// Advances frames until <paramref name="condition"/> holds, or fails the test.
        /// </summary>
        /// <remarks>
        /// A bounded wait rather than a fixed number of frames. Frame timing varies with what else
        /// the editor is doing, so a test that waits exactly 40 frames for a 0.55 second wind-up is
        /// a test that fails on a slow machine and proves nothing on a fast one.
        /// </remarks>
        protected static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            float deadline = Time.time + timeoutSeconds;

            while (!condition())
            {
                if (Time.time > deadline)
                {
                    Assert.Fail($"Timed out after {timeoutSeconds:0.##}s waiting for {what}.");
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>Advances frames for a fixed span, for "and nothing happened" assertions.</summary>
        protected static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;

            while (Time.time < until)
            {
                yield return null;
            }
        }
    }
}
