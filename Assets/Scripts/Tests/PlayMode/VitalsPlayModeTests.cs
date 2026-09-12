using System.Collections;
using Frieren.Characters;
using Frieren.Core;
using Frieren.Core.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Regeneration delays and the barrier's lapse: the parts of the vitals that only exist in time.
    /// </summary>
    public sealed class VitalsPlayModeTests : PlayModeTestBase
    {
        private GameObject character;

        private CharacterHealth SpawnWithHealthRegen(float perSecond, float delay)
        {
            character = World.CreateCharacter("Regen", new Vector3(0f, 0.1f, 0f), GameLayers.Player,
                World.CreateStats(healthRegenPerSecond: perSecond, healthRegenDelay: delay));
            return character.GetComponent<CharacterHealth>();
        }

        [UnityTest]
        public IEnumerator HealthRegeneratesAfterItsDelay()
        {
            CharacterHealth health = SpawnWithHealthRegen(perSecond: 20f, delay: 0.4f);
            health.TakeDamage(new DamageInfo(50f));

            yield return Wait(0.2f);
            Assert.AreEqual(50f, health.Current, 0.5f, "Regeneration must pause after being hit.");

            yield return Wait(0.9f);
            Assert.Greater(health.Current, 55f, "Once the delay is up it should be climbing.");
        }

        [UnityTest]
        public IEnumerator RegenerationStopsAtFull()
        {
            CharacterHealth health = SpawnWithHealthRegen(perSecond: 200f, delay: 0.1f);
            health.TakeDamage(new DamageInfo(10f));

            yield return Wait(1f);

            Assert.AreEqual(health.Max, health.Current, 0.01f);
        }

        [UnityTest]
        public IEnumerator TakingDamageRestartsTheDelay()
        {
            CharacterHealth health = SpawnWithHealthRegen(perSecond: 30f, delay: 0.5f);
            health.TakeDamage(new DamageInfo(50f));

            yield return Wait(0.35f);
            health.TakeDamage(new DamageInfo(5f));
            yield return Wait(0.35f);

            Assert.AreEqual(45f, health.Current, 1.5f,
                "The second hit should have pushed regeneration back out again.");
        }

        [UnityTest]
        public IEnumerator ManaRegeneratesAfterSpending()
        {
            character = World.CreateCharacter("Caster", new Vector3(0f, 0.1f, 0f), GameLayers.Player,
                World.CreateStats(maxMana: 100f, manaRegenPerSecond: 40f, manaRegenDelay: 0.3f));
            CharacterMana mana = character.GetComponent<CharacterMana>();

            Assert.IsTrue(mana.TrySpend(60f));
            Assert.AreEqual(40f, mana.Current, 0.01f);

            yield return Wait(0.15f);
            Assert.AreEqual(40f, mana.Current, 0.5f, "Spending pauses regeneration; that is the casting pace lever.");

            yield return Wait(0.8f);
            Assert.Greater(mana.Current, 55f);
        }

        [UnityTest]
        public IEnumerator TheBarrierLapsesAfterThePulsesStop()
        {
            character = World.CreateCharacter("Warded", new Vector3(0f, 0.1f, 0f), GameLayers.Player);
            CharacterBarrier barrier = character.AddComponent<CharacterBarrier>();

            barrier.ReceiveMagic(new MagicPulse(MagicElement.Warding, 10f, Vector3.zero));
            Assert.IsTrue(barrier.IsUp);

            yield return WaitUntil(() => !barrier.IsUp, 1.5f, "the barrier to lapse");
        }

        [UnityTest]
        public IEnumerator RepulsingTheBarrierKeepsItUp()
        {
            character = World.CreateCharacter("Warded", new Vector3(0f, 0.1f, 0f), GameLayers.Player);
            CharacterBarrier barrier = character.AddComponent<CharacterBarrier>();

            // A channel refreshes every tick. Well inside the 0.35s lapse delay.
            for (int i = 0; i < 8; i++)
            {
                barrier.ReceiveMagic(new MagicPulse(MagicElement.Warding, 10f, Vector3.zero));
                yield return Wait(0.15f);
            }

            Assert.IsTrue(barrier.IsUp, "A held barrier must not lapse while it is being held.");
        }

        [UnityTest]
        public IEnumerator TheBarrierAbsorbsARealHitInPlay()
        {
            character = World.CreateCharacter("Warded", new Vector3(0f, 0.1f, 0f), GameLayers.Player);
            CharacterBarrier barrier = character.AddComponent<CharacterBarrier>();
            CharacterHealth health = character.GetComponent<CharacterHealth>();

            // Health caches its modifiers in Awake, which already ran; a barrier added afterwards
            // has to announce itself. If this is ever needed in real code, that is a bug worth
            // knowing about - here it is just the cost of building a character a piece at a time.
            health.CacheModifiers();

            barrier.ReceiveMagic(new MagicPulse(MagicElement.Warding, 10f, Vector3.zero));
            health.TakeDamage(new DamageInfo(25f));

            yield return null;

            Assert.AreEqual(health.Max, health.Current, 0.01f, "The ward should have eaten all of it.");
            Assert.Less(barrier.Remaining, 60f);
        }

        [UnityTest]
        public IEnumerator LevitationHoldsTheCharacterUpAndThenLetsItDown()
        {
            character = World.CreateCharacter("Floater", new Vector3(0f, 0.1f, 0f), GameLayers.Player);
            CharacterLevitation levitation = character.AddComponent<CharacterLevitation>();
            CharacterMotor motor = character.GetComponent<CharacterMotor>();

            yield return WaitUntil(() => motor.IsGrounded, 2f, "the character to settle on the ground");

            float groundHeight = character.transform.position.y;

            // A channel delivers a pulse per tick; this stands in for holding the button.
            for (int i = 0; i < 12; i++)
            {
                levitation.ReceiveMagic(new MagicPulse(MagicElement.Force, 10f, character.transform.position));
                yield return Wait(0.1f);
            }

            Assert.IsTrue(levitation.IsLevitating);
            Assert.Greater(character.transform.position.y, groundHeight + 0.5f,
                "Holding the spell should actually lift the body, not merely set a flag.");
            Assert.IsFalse(motor.GravityEnabled, "Gravity must be suspended while aloft.");

            yield return WaitUntil(() => !levitation.IsLevitating, 3f, "levitation to end after release");
            yield return WaitUntil(() => motor.GravityEnabled, 2f, "gravity to come back");
            yield return WaitUntil(() => motor.IsGrounded, 5f, "the character to land");
        }
    }
}
