using System.Collections.Generic;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.EditMode
{
    public sealed class SliceObjectivesTests
    {
        private GameObject host;
        private SliceObjectives objectives;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Objectives");
            objectives = host.AddComponent<SliceObjectives>();

            var goals = new List<SliceObjectives.Goal>
            {
                new SliceObjectives.Goal { id = "one", description = "First" },
                new SliceObjectives.Goal { id = "two", description = "Second" },
                new SliceObjectives.Goal { id = "three", description = "Third" },
            };

            // Reflection rather than the play-mode assembly's TestFields helper: the two test
            // assemblies cannot see each other, and one call does not justify a third assembly.
            typeof(SliceObjectives)
                .GetField("goals", System.Reflection.BindingFlags.Instance |
                                   System.Reflection.BindingFlags.NonPublic)
                .SetValue(objectives, goals);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void NothingStartsDone()
        {
            Assert.IsFalse(objectives.IsComplete);
            Assert.IsFalse(objectives.IsDone("one"));
        }

        [Test]
        public void CompletingAGoalMarksItDone()
        {
            Assert.IsTrue(objectives.Complete("two"));
            Assert.IsTrue(objectives.IsDone("two"));
            Assert.IsFalse(objectives.IsDone("one"), "Only the one that was completed.");
        }

        [Test]
        public void AGoalCompletesOnlyOnce()
        {
            objectives.Complete("one");

            Assert.IsFalse(objectives.Complete("one"), "A second report is not a second completion.");
        }

        [Test]
        public void OrderDoesNotMatter()
        {
            // A player who levitates over the gate and skips ahead has solved the level, not
            // broken it. Refusing an out-of-order goal would contradict the whole design.
            Assert.IsTrue(objectives.Complete("three"));
            Assert.IsTrue(objectives.IsDone("three"));
            Assert.IsFalse(objectives.IsComplete);
        }

        [Test]
        public void FinishingEveryGoalCompletesTheSlice()
        {
            bool announced = false;
            objectives.AllGoalsCompleted += () => announced = true;

            objectives.Complete("one");
            objectives.Complete("two");
            Assert.IsFalse(objectives.IsComplete);

            objectives.Complete("three");

            Assert.IsTrue(objectives.IsComplete);
            Assert.IsTrue(announced);
        }

        [Test]
        public void AnUnknownGoalIsRefused()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("nonsense"));

            Assert.IsFalse(objectives.Complete("nonsense"));
            Assert.IsFalse(objectives.IsComplete);
        }

        [Test]
        public void EachCompletionIsAnnouncedOnce()
        {
            var seen = new List<string>();
            objectives.GoalCompleted += seen.Add;

            objectives.Complete("one");
            objectives.Complete("one");
            objectives.Complete("two");

            CollectionAssert.AreEqual(new[] { "one", "two" }, seen);
        }

        [Test]
        public void ProgressSurvivesASaveAndLoad()
        {
            objectives.Complete("one");
            objectives.Complete("three");

            string saved = objectives.CaptureState();

            objectives.RestoreState("{\"completed\":[]}");
            Assert.IsFalse(objectives.IsDone("one"), "The reset must actually have reset it.");

            objectives.RestoreState(saved);

            Assert.IsTrue(objectives.IsDone("one"));
            Assert.IsTrue(objectives.IsDone("three"));
            Assert.IsFalse(objectives.IsDone("two"));
        }

        [Test]
        public void RestoringGarbageChangesNothing()
        {
            objectives.Complete("one");
            objectives.RestoreState("");

            Assert.IsTrue(objectives.IsDone("one"));
        }
    }
}
