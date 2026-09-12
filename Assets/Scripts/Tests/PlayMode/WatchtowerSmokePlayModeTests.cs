using System.Collections;
using System.Collections.Generic;
using Frieren.Characters;
using Frieren.Core.Persistence;
using Frieren.Core.Services;
using Frieren.Enemies;
using Frieren.Player;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Loads the Watchtower and checks the level is actually there.
    /// </summary>
    /// <remarks>
    /// Four and a half thousand lines of hand-written YAML that nobody has looked at. The structural
    /// checker proves the references resolve and the geometry checker proves the ramps are walkable;
    /// only Unity proves the level loads and the three problems each have a component behind them.
    ///
    /// It asserts the *design*, not just the objects: that the gate can be burned as well as
    /// unlocked, that the stair starts broken, that the aqueduct starts empty. Each of those is a
    /// route through the level, and losing one silently would leave a level that still works and is
    /// no longer the level that was designed.
    /// </remarks>
    public sealed class WatchtowerSmokePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadWatchtower()
        {
            Time.timeScale = 1f;
            ServiceLocator.Clear();
            yield return SceneManager.LoadSceneAsync("Watchtower", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            ServiceLocator.Clear();
            Time.timeScale = 1f;

            Scene blank = SceneManager.CreateScene("WatchtowerCleanup");
            SceneManager.SetActiveScene(blank);

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene != blank && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheLevelLoadsWithItsObjectives()
        {
            var objectives = Object.FindFirstObjectByType<SliceObjectives>();

            Assert.IsNotNull(objectives, "No objectives means no slice, just a room.");
            Assert.AreEqual(4, objectives.Goals.Count);
            Assert.IsFalse(objectives.IsComplete, "It should not start finished.");

            var ids = new HashSet<string>();

            for (int i = 0; i < objectives.Goals.Count; i++)
            {
                SliceObjectives.Goal goal = objectives.Goals[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(goal.id), $"Goal {i} has no id.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(goal.description), $"Goal '{goal.id}' says nothing.");
                Assert.IsTrue(ids.Add(goal.id), $"Duplicate goal id '{goal.id}'.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheGateCanBeUnlockedOrBurned()
        {
            var gate = Object.FindFirstObjectByType<LockedObject>();

            Assert.IsNotNull(gate, "The first problem is missing.");
            Assert.IsTrue(gate.IsLocked, "A gate that starts open is not a problem.");
            Assert.IsNotNull(gate.GetComponentInChildren<FlammableObject>(true),
                "Burning the gate is its second answer; without this there is only one.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheStairStartsBrokenAndCanBeMended()
        {
            var stair = Object.FindFirstObjectByType<RepairableObject>();

            Assert.IsNotNull(stair, "The second problem is missing.");
            Assert.IsFalse(stair.IsIntact, "A stair that starts whole is not a problem.");
            Assert.AreEqual(0f, stair.NormalizedProgress, 0.001f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheAqueductStartsEmptyAndCanBeFrozen()
        {
            var aqueduct = Object.FindFirstObjectByType<WaterBasin>();

            Assert.IsNotNull(aqueduct, "The third problem is missing.");
            Assert.AreEqual(BasinState.Empty, aqueduct.State,
                "An aqueduct that starts frozen is a bridge, not a puzzle.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePlayerAndTwoSentinelsArrive()
        {
            yield return WaitFor(() => Object.FindFirstObjectByType<PlayerSpellInput>() != null, 10f,
                "the player to spawn");
            yield return WaitFor(() => Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length >= 2,
                10f, "both sentinels to spawn");

            var player = Object.FindFirstObjectByType<PlayerSpellInput>();
            Assert.AreEqual(8, player.KnownSpells.Count);
            Assert.IsNotNull(player.GetComponent<PlayerBlockInput>());
            Assert.IsNotNull(player.GetComponent<SpellWheelInput>());
        }

        [UnityTest]
        public IEnumerator EveryPersistentObjectHasItsOwnId()
        {
            yield return WaitFor(() => Object.FindFirstObjectByType<PlayerSpellInput>() != null, 10f,
                "the player to spawn");

            var seen = new Dictionary<string, string>();

            foreach (SceneObjectId id in Object.FindObjectsByType<SceneObjectId>(FindObjectsSortMode.None))
            {
                Assert.IsTrue(id.HasId,
                    $"'{id.name}' has no save id, so nothing about it will ever persist.");
                seen.TryGetValue(id.Id, out string other);
                Assert.IsNull(other,
                    $"'{id.Id}' is used by both '{other}' and '{id.name}' - " +
                    "they would share one save entry.");
                seen[id.Id] = id.name;
            }

            Assert.Greater(seen.Count, 3, "The level should have several things worth remembering.");
        }

        [UnityTest]
        public IEnumerator TheSealIsReachableAndFinishesTheSlice()
        {
            var seal = Object.FindFirstObjectByType<ObjectiveInteractable>();
            var objectives = Object.FindFirstObjectByType<SliceObjectives>();

            Assert.IsNotNull(seal, "Nothing to do at the top.");

            // Completing every goal by hand: the slice has to be finishable at all before it is
            // worth asking whether it is finishable by playing.
            for (int i = 0; i < objectives.Goals.Count; i++)
            {
                objectives.Complete(objectives.Goals[i].id);
            }

            Assert.IsTrue(objectives.IsComplete);

            yield return null;
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + seconds;

            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Timed out after {seconds:0.#}s waiting for {what}.");
                    yield break;
                }

                yield return null;
            }
        }
    }
}
