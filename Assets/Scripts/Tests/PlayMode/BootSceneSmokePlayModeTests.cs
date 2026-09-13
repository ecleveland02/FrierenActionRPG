using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Frieren.Characters;
using Frieren.Core.Bootstrap;
using Frieren.Core.Services;
using Frieren.Core.Timing;
using Frieren.Enemies;
using Frieren.Magic;
using Frieren.Player;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Boots the real project and checks that the hand-authored scenes, prefabs and assets loaded.
    /// </summary>
    /// <remarks>
    /// The one test here that exercises the YAML rather than the code. Every scene, prefab and asset
    /// in this project was written by a generator script rather than by the Unity editor, so the
    /// usual safety net - the editor refusing to save something malformed - does not exist. A
    /// structural checker (<c>Tools/Validation/check_unity_yaml.py</c>) catches broken references;
    /// only Unity can say whether a hand-written enum index deserialised into the field someone
    /// meant. The spell assertions below are aimed squarely at that.
    ///
    /// It does not inherit <see cref="PlayModeTestBase"/>: that fixture builds a throwaway world,
    /// and this one wants the real one. It cleans up after itself instead, because
    /// <c>Bootstrapper</c> survives scene loads by design and would otherwise follow later tests
    /// around.
    ///
    /// If this fixture ever proves flaky in a way the others are not, it is the one to disable -
    /// everything else runs without loading a scene at all.
    /// </remarks>
    public sealed class BootSceneSmokePlayModeTests
    {
        private const float BootTimeout = 20f;

        [UnitySetUp]
        public IEnumerator LoadBoot()
        {
            Time.timeScale = 1f;
            // Each case represents a fresh launch, not another Boot loaded behind the
            // bootstrap guard that the test runner created for its own starting scene.
            if (Bootstrapper.Instance != null)
            {
                Object.Destroy(Bootstrapper.Instance.gameObject);
                yield return null;
            }
            MethodInfo resetGuard = typeof(SceneBootstrapGuard).GetMethod("ResetState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(resetGuard, "The editor bootstrap reset must be available for a fresh-launch test.");
            resetGuard.Invoke(null, null);
            ServiceLocator.Clear();

            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return WaitForPlayer();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Bootstrapper.Instance != null)
            {
                Object.Destroy(Bootstrapper.Instance.gameObject);
            }

            ServiceLocator.Clear();
            Time.timeScale = 1f;

            // A scene of its own to land in, so the next fixture does not inherit the test scene's
            // colliders, spawners and lighting.
            Scene blank = SceneManager.CreateScene("PlayModeCleanup");
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

        private static IEnumerator WaitForPlayer()
        {
            float deadline = Time.realtimeSinceStartup + BootTimeout;

            while (FindPlayer() == null)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"No player after {BootTimeout}s. Boot did not reach the test scene, " +
                                "or PlayerSpawner did not run.");
                    yield break;
                }

                yield return null;
            }
        }

        private static PlayerSpellInput FindPlayer() => Object.FindFirstObjectByType<PlayerSpellInput>();

        [UnityTest]
        public IEnumerator TheProjectBootsAndSpawnsThePlayer()
        {
            PlayerSpellInput player = FindPlayer();

            Assert.IsNotNull(player);
            Assert.IsNotNull(player.GetComponent<CharacterSpellcaster>());
            Assert.IsNotNull(player.GetComponent<CharacterBarrier>(), "Barrier magic needs somewhere to land.");
            Assert.IsNotNull(player.GetComponent<CharacterLevitation>());

            CharacterStats stats = player.GetComponent<CharacterStats>();
            Assert.IsTrue(stats.HasDefinition, "The player prefab must carry a stats archetype.");
            Assert.Greater(stats.MaxHealth, 0f);
            Assert.Greater(stats.MaxMana, 0f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator EverySpellOnThePlayerIsWiredUp()
        {
            PlayerSpellInput player = FindPlayer();
            IReadOnlyList<SpellDefinition> spells = player.KnownSpells;

            Assert.AreEqual(8, spells.Count,
                "Eight spells are on the wheel. Barrier is the ninth and lives on the block button.");

            var ids = new HashSet<string>();

            for (int i = 0; i < spells.Count; i++)
            {
                SpellDefinition spell = spells[i];

                Assert.IsNotNull(spell, $"Spell slot {i + 1} is empty.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(spell.Id), $"{spell.name} has no id.");
                Assert.IsTrue(ids.Add(spell.Id), $"Duplicate spell id '{spell.Id}'.");

                // The assertion this fixture exists for. Effects are asset references written by
                // hand into YAML; a wrong GUID leaves an empty list and a spell that silently does
                // nothing at all.
                Assert.IsTrue(spell.HasEffects, $"{spell.Id} has no effects and would do nothing.");
                Assert.GreaterOrEqual(spell.Range, 0f, $"{spell.Id} has a negative range.");

                if (spell.IsChannelled)
                {
                    Assert.Greater(spell.ManaPerSecond, 0f,
                        $"{spell.Id} is channelled but drains nothing, so it could be held forever.");
                    Assert.Greater(spell.ChannelTickInterval, 0f, $"{spell.Id} has no tick interval.");
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ZoltraakIsWhatItClaimsToBe()
        {
            PlayerSpellInput player = FindPlayer();
            SpellDefinition zoltraak = FindSpell(player, "spell.zoltraak");

            Assert.AreEqual(SpellTargeting.Ray, zoltraak.Targeting, "Zoltraak is aimed, not self-cast.");
            Assert.IsFalse(zoltraak.IsChannelled);
            Assert.Greater(zoltraak.Range, 20f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator BlockingIsWiredToItsOwnButtonAndNotToTheWheel()
        {
            PlayerSpellInput player = FindPlayer();
            var block = player.GetComponent<PlayerBlockInput>();

            Assert.IsNotNull(block, "Right mouse has to reach something.");
            Assert.IsNotNull(block.BlockSpell, "The block button has no spell behind it.");
            Assert.AreEqual("spell.barrier", block.BlockSpell.Id);
            Assert.AreEqual(SpellTargeting.Self, block.BlockSpell.Targeting, "A ward goes on the caster.");
            Assert.IsTrue(block.BlockSpell.IsChannelled, "Blocking is held, not fired once.");

            for (int i = 0; i < player.KnownSpells.Count; i++)
            {
                Assert.AreNotEqual("spell.barrier", player.KnownSpells[i]?.Id,
                    "Barrier moved off the wheel; leaving it in a slot too would be two ways to do one thing.");
            }

            Assert.IsNotNull(player.GetComponent<SpellWheelInput>(), "Q has to reach something.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheEnemySpawnsAndTheServicesAreRegistered()
        {
            yield return WaitUntilOrFail(() => Object.FindFirstObjectByType<EnemyBrain>() != null, 5f,
                "the enemy spawner to place a sentinel");

            EnemyBrain enemy = Object.FindFirstObjectByType<EnemyBrain>();
            Assert.IsNotNull(enemy.GetComponent<EnemyPerception>());
            Assert.IsNotNull(enemy.GetComponent<EnemyMelee>());
            Assert.IsTrue(enemy.GetComponent<CharacterStats>().HasDefinition);

            Assert.IsTrue(ServiceLocator.IsRegistered<TimeScaleService>(),
                "Nothing else may write Time.timeScale, so this must exist.");
            Assert.IsTrue(ServiceLocator.IsRegistered<IScreenShake>(),
                "The camera registers itself; a missing one means the rig is not in the scene.");
        }

        [UnityTest]
        public IEnumerator TheEnvironmentalPuzzlesArePresent()
        {
            Assert.IsNotNull(Object.FindFirstObjectByType<WaterBasin>(), "The trough is missing.");
            Assert.IsNotNull(Object.FindFirstObjectByType<RepairableObject>(), "The broken pillar is missing.");
            Assert.IsNotNull(Object.FindFirstObjectByType<LockedObject>(), "The locked door is missing.");
            Assert.IsNotNull(Object.FindFirstObjectByType<FlammableObject>(), "The flammable props are missing.");

            // The door's second solution. It is only a second solution if the panel can burn.
            LockedObject door = Object.FindFirstObjectByType<LockedObject>();
            Assert.IsNotNull(door.GetComponentInChildren<FlammableObject>(true),
                "The door should be burnable as well as unlockable - that is the multiple-solutions pillar.");

            yield return null;
        }

        private static SpellDefinition FindSpell(PlayerSpellInput player, string id)
        {
            for (int i = 0; i < player.KnownSpells.Count; i++)
            {
                if (player.KnownSpells[i] != null && player.KnownSpells[i].Id == id)
                {
                    return player.KnownSpells[i];
                }
            }

            Assert.Fail($"The player does not know '{id}'.");
            return null;
        }

        private static IEnumerator WaitUntilOrFail(System.Func<bool> condition, float seconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + seconds;

            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Timed out after {seconds:0.##}s waiting for {what}.");
                    yield break;
                }

                yield return null;
            }
        }
    }
}
