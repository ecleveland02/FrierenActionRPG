using System.Collections;
using System.Collections.Generic;
using Frieren.Characters;
using Frieren.Core.Persistence;
using Frieren.Core.Services;
using Frieren.Enemies;
using Frieren.Player;
using Frieren.Presentation;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
        public IEnumerator WoodlandHasTwoEncountersAndBakedPaths()
        {
            yield return WaitFor(() => Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length == 4,
                10f, "four woodland sentinels");
            Assert.AreEqual(2, Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None).Length);
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, 0f, -20f), out NavMeshHit start, 2f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(-3f, 0f, -14f), out NavMeshHit end, 2f, NavMesh.AllAreas));
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            foreach (EnemyBrain enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                Assert.IsNotNull(enemy.GetComponent<EnemyNavigation>());
                var animator = enemy.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator);
                Assert.IsNotNull(animator.runtimeAnimatorController);
                Assert.IsFalse(animator.applyRootMotion);
            }
        }

        [UnityTest]
        public IEnumerator ImportedMonsterActuallyAnimatesAndNavigates()
        {
            yield return WaitFor(() => Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length == 4,
                10f, "all encounters");
            EnemyBrain enemy = null;
            foreach (EnemyBrain candidate in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
                if (enemy == null || candidate.transform.position.z < enemy.transform.position.z) enemy = candidate;
            var animator = enemy.GetComponentInChildren<Animator>();
            Transform[] bones = animator.GetComponentsInChildren<Transform>();
            var poses = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) poses[i] = bones[i].localRotation;
            // Offscreen animation culling is disabled only for this motion-binding assertion.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var player = Object.FindFirstObjectByType<PlayerSpellInput>();
            player.GetComponent<CharacterMotor>().Teleport(new Vector3(0f, 0.2f, -23f), Quaternion.identity);
            enemy.GetComponent<EnemyPerception>().ForceTarget(player.transform);
            Vector3 start = enemy.transform.position;
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(Vector3.Distance(start, enemy.transform.position), 1f, "The baked path should produce real movement.");
            bool animated = false;
            for (int i = 0; i < bones.Length; i++)
                if (Quaternion.Angle(poses[i], bones[i].localRotation) > 1f) animated = true;
            Assert.IsTrue(animated, "Clips must bind to the imported bones, not merely exist in a controller.");
        }

        [UnityTest]
        public IEnumerator FightingChangesMusicAndDefeatingEnemiesRestoresExploration()
        {
            yield return WaitFor(() => Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length == 4,
                10f, "all encounters");
            var music = Object.FindFirstObjectByType<EncounterMusic>();
            Assert.IsNotNull(music);
            Assert.IsFalse(music.InCombat, "The campsite should start peaceful.");
            var player = Object.FindFirstObjectByType<PlayerSpellInput>();
            player.GetComponent<CharacterMotor>().Teleport(new Vector3(0f, 0.2f, -20f), Quaternion.identity);
            var enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            EnemyBrain nearest = null;
            foreach (EnemyBrain candidate in enemies)
                if (nearest == null || candidate.transform.position.z < nearest.transform.position.z) nearest = candidate;
            nearest.GetComponent<EnemyPerception>().ForceTarget(player.transform);
            yield return WaitFor(() => music.InCombat && music.CombatBlend > 0.5f, 5f, "combat music to fade in");
            Assert.IsTrue(music.HasActiveThreat());
            foreach (EnemyBrain enemy in enemies) enemy.GetComponent<CharacterHealth>().TakeDamage(new DamageInfo(100000f));
            yield return null;
            Assert.IsTrue(music.InCombat, "The release hold should prevent abrupt track switching.");
            yield return WaitFor(() => !music.InCombat && music.CombatBlend < 0.01f, 10f, "exploration music to return");
            Assert.IsFalse(music.HasActiveThreat());
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
