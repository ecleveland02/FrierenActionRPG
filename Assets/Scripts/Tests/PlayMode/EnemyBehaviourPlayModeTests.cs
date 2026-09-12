using System.Collections;
using Frieren.Characters;
using Frieren.Core;
using Frieren.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// The enemy, end to end. None of this was covered before: every part of it needs a running
    /// clock, a physics scene, or both, and edit-mode tests have neither.
    /// </summary>
    public sealed class EnemyBehaviourPlayModeTests : PlayModeTestBase
    {
        private GameObject player;
        private GameObject enemy;
        private EnemyBrain brain;
        private EnemyMelee melee;
        private EnemyPerception perception;
        private CharacterHealth playerHealth;
        private CharacterHealth enemyHealth;

        private void Spawn(float apart, float enemyHealthPoints = 100f)
        {
            // The enemy faces +Z by default, so the player goes in front of it and inside the cone.
            enemy = World.CreateEnemy("Sentinel", new Vector3(0f, 0.1f, 0f),
                World.CreateStats(maxHealth: enemyHealthPoints, maxMana: 0f));
            player = World.CreateCharacter("Player", new Vector3(0f, 0.1f, apart), GameLayers.Player);

            brain = enemy.GetComponent<EnemyBrain>();
            melee = enemy.GetComponent<EnemyMelee>();
            perception = enemy.GetComponent<EnemyPerception>();
            enemyHealth = enemy.GetComponent<CharacterHealth>();
            playerHealth = player.GetComponent<CharacterHealth>();
        }

        [UnityTest]
        public IEnumerator ItIgnoresNothing()
        {
            enemy = World.CreateEnemy("Sentinel", new Vector3(0f, 0.1f, 0f));
            brain = enemy.GetComponent<EnemyBrain>();

            yield return Wait(0.6f);

            Assert.AreEqual(EnemyState.Idle, brain.State, "With no player in the scene it should stand still.");
        }

        [UnityTest]
        public IEnumerator ItNoticesAPlayerInFront()
        {
            Spawn(apart: 8f);

            yield return WaitUntil(() => perception.HasTarget, 2f, "the enemy to notice the player");

            Assert.AreSame(player.transform, perception.Target);
        }

        [UnityTest]
        public IEnumerator ItDoesNotNoticeAPlayerBeyondSightRange()
        {
            Spawn(apart: 18f);

            yield return Wait(1.5f);

            Assert.IsFalse(perception.HasTarget, "Sight range is 14m; 18m should not register.");
            Assert.AreEqual(EnemyState.Idle, brain.State);
        }

        [UnityTest]
        public IEnumerator ItDoesNotNoticeAPlayerBehindIt()
        {
            Spawn(apart: 8f);
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            yield return Wait(1.5f);

            Assert.IsFalse(perception.HasTarget, "The field of view is 140 degrees, not 360.");
        }

        [UnityTest]
        public IEnumerator ItClosesTheDistance()
        {
            Spawn(apart: 9f);

            yield return WaitUntil(() => brain.State == EnemyState.Chase, 2f, "the chase to start");

            float startDistance = perception.PlanarDistanceToTarget();

            yield return Wait(1f);

            Assert.Less(perception.PlanarDistanceToTarget(), startDistance - 1f,
                "A second of chasing at 3.2 m/s should close well over a metre.");
        }

        [UnityTest]
        public IEnumerator ItStopsWithinReachAndSwings()
        {
            Spawn(apart: 7f);

            yield return WaitUntil(() => brain.State == EnemyState.Attack, 6f, "the enemy to reach the player");

            Assert.LessOrEqual(perception.PlanarDistanceToTarget(), melee.Range + 0.5f,
                "It should stop inside its own reach rather than walking through the player.");

            yield return WaitUntil(() => melee.IsWindingUp, 3f, "a swing to begin");
        }

        [UnityTest]
        public IEnumerator TheWindUpHappensBeforeAnyDamage()
        {
            Spawn(apart: 4f);

            yield return WaitUntil(() => melee.IsWindingUp, 8f, "the wind-up");

            Assert.AreEqual(playerHealth.Max, playerHealth.Current, 0.01f,
                "Damage during the wind-up would make the telegraph a lie and the dodge window fake.");

            yield return WaitUntil(() => playerHealth.Current < playerHealth.Max, 3f, "the swing to land");
        }

        [UnityTest]
        public IEnumerator ItCannotHitAPlayerWhoLeftDuringTheWindUp()
        {
            Spawn(apart: 4f);

            yield return WaitUntil(() => melee.IsWindingUp, 8f, "the wind-up");

            // What the wind-up is for. Teleporting rather than walking, because this is a test of
            // the melee window and not of the locomotion.
            player.GetComponent<CharacterMotor>().Teleport(new Vector3(0f, 0.1f, 25f), Quaternion.identity);

            yield return Wait(1.2f);

            Assert.AreEqual(playerHealth.Max, playerHealth.Current, 0.01f,
                "Leaving during the wind-up must avoid the hit, or the telegraph means nothing.");
        }

        [UnityTest]
        public IEnumerator BeingHitStaggersItAndAbortsTheSwing()
        {
            Spawn(apart: 4f);

            yield return WaitUntil(() => melee.IsSwinging, 8f, "a swing to start");

            enemyHealth.TakeDamage(new DamageInfo(5f, DamageType.Arcane, player));

            yield return null;

            Assert.AreEqual(EnemyState.Stagger, brain.State);
            Assert.IsFalse(melee.IsSwinging, "A stagger must abort the swing, not run alongside it.");
        }

        [UnityTest]
        public IEnumerator ItRecoversFromAStagger()
        {
            Spawn(apart: 6f);

            yield return WaitUntil(() => brain.State == EnemyState.Chase, 3f, "the chase");

            enemyHealth.TakeDamage(new DamageInfo(5f, DamageType.Arcane, player));

            yield return WaitUntil(() => brain.State == EnemyState.Stagger, 0.5f, "the stagger");
            yield return WaitUntil(() => brain.State != EnemyState.Stagger, 2f, "recovery");

            Assert.AreEqual(EnemyState.Chase, brain.State, "It should go back to what it was doing.");
        }

        [UnityTest]
        public IEnumerator RepeatedHitsCannotLockItInPlace()
        {
            Spawn(apart: 6f);

            yield return WaitUntil(() => brain.State == EnemyState.Chase, 3f, "the chase");

            // Nine hits over 1.5s. With a 0.9s stagger cooldown at most two of them may stagger.
            for (int i = 0; i < 9; i++)
            {
                enemyHealth.TakeDamage(new DamageInfo(1f, DamageType.Arcane, player));
                yield return Wait(0.17f);
            }

            yield return WaitUntil(() => brain.State != EnemyState.Stagger, 1.5f,
                "the enemy to break out of a stun-lock");
        }

        [UnityTest]
        public IEnumerator DeathStopsItAndReleasesTheLock()
        {
            Spawn(apart: 5f, enemyHealthPoints: 20f);

            yield return WaitUntil(() => brain.State == EnemyState.Chase, 3f, "the chase");

            enemyHealth.TakeDamage(new DamageInfo(100f, DamageType.Arcane, player));

            yield return null;

            Assert.AreEqual(EnemyState.Dead, brain.State);
            Assert.IsFalse(enemyHealth.IsAlive);
            Assert.IsFalse(enemy.GetComponent<CharacterActionLock>().IsLocked,
                "A corpse holding the action lock would deadlock anything that reuses the body.");
        }

        [UnityTest]
        public IEnumerator ADeadEnemyStopsDealingDamage()
        {
            Spawn(apart: 4f, enemyHealthPoints: 20f);

            yield return WaitUntil(() => melee.IsWindingUp, 8f, "the wind-up");

            enemyHealth.TakeDamage(new DamageInfo(100f, DamageType.Arcane, player));

            yield return Wait(1.2f);

            Assert.AreEqual(playerHealth.Max, playerHealth.Current, 0.01f,
                "Killing it mid-swing must cancel the swing.");
        }

        [UnityTest]
        public IEnumerator BeingShotFromBehindTurnsItAround()
        {
            Spawn(apart: 8f);
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            yield return Wait(0.6f);
            Assert.IsFalse(perception.HasTarget, "It should not have seen the player yet.");

            enemyHealth.TakeDamage(new DamageInfo(5f, DamageType.Arcane, player));

            yield return WaitUntil(() => perception.HasTarget, 1f, "the enemy to acquire its attacker");
            Assert.AreSame(player.transform, perception.Target);
        }
    }
}
