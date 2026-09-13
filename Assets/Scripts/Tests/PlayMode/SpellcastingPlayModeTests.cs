using System.Collections;
using Frieren.Characters;
using Frieren.Core;
using Frieren.Core.Magic;
using Frieren.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Channelling, mana drain and cooldowns. All of it turns on a clock, so none of it was covered.
    /// </summary>
    public sealed class SpellcastingPlayModeTests : PlayModeTestBase
    {
        private GameObject caster;
        private CharacterSpellcaster spellcaster;
        private CharacterMana mana;
        private CharacterActionLock actionLock;

        private void SpawnCaster(float maxMana = 100f)
        {
            caster = World.CreateCharacter("Caster", new Vector3(0f, 0.1f, 0f), GameLayers.Player,
                World.CreateStats(maxMana: maxMana, manaRegenPerSecond: 0f));
            spellcaster = caster.AddComponent<CharacterSpellcaster>();
            mana = caster.GetComponent<CharacterMana>();
            actionLock = caster.GetComponent<CharacterActionLock>();
        }

        private SpellDefinition Channelled(string id, float manaCost, float manaPerSecond,
            float tickInterval, bool holdsLock, SpellTargeting targeting, params SpellEffect[] effects)
        {
            SpellDefinition spell = World.CreateSpell(id, targeting, effects);
            TestFields.Set(spell, "castMode", SpellCastMode.Channelled);
            TestFields.Set(spell, "manaCost", manaCost);
            TestFields.Set(spell, "manaPerSecond", manaPerSecond);
            TestFields.Set(spell, "channelTickInterval", tickInterval);
            TestFields.Set(spell, "holdsActionLock", holdsLock);
            TestFields.Set(spell, "castTime", 0.05f);
            return spell;
        }

        [UnityTest]
        public IEnumerator AnInstantCastSpendsManaOnce()
        {
            SpawnCaster();
            SpellDefinition spell = World.CreateSpell("test.bolt", SpellTargeting.Ray,
                World.CreateDamageEffect(10f));
            TestFields.Set(spell, "manaCost", 20f);

            Assert.IsTrue(spellcaster.TryCast(spell));

            yield return WaitUntil(() => !spellcaster.IsCasting, 2f, "the cast to finish");

            Assert.AreEqual(80f, mana.Current, 0.01f);
        }

        [UnityTest]
        public IEnumerator ACastIsRefusedWithoutTheMana()
        {
            SpawnCaster(maxMana: 10f);
            SpellDefinition spell = World.CreateSpell("test.bolt", SpellTargeting.Ray,
                World.CreateDamageEffect(10f));
            TestFields.Set(spell, "manaCost", 40f);

            string refusal = null;
            spellcaster.CastRefused += (_, reason) => refusal = reason;

            Assert.IsFalse(spellcaster.TryCast(spell));

            yield return null;

            Assert.IsNotNull(refusal, "A refusal must say why, or the player just sees nothing happen.");
            Assert.AreEqual(10f, mana.Current, 0.01f);
        }

        [UnityTest]
        public IEnumerator AChannelDrainsManaWhileItIsHeld()
        {
            SpawnCaster();
            SpellDefinition spell = Channelled("test.channel", 10f, 20f, 0.1f, false,
                SpellTargeting.Self, World.CreatePulseEffect(MagicElement.Warding, 10f));

            spellcaster.TryCast(spell);

            yield return WaitUntil(() => spellcaster.IsChannelling, 1f, "the channel to begin");

            float afterStart = mana.Current;

            yield return Wait(0.5f);

            Assert.Less(mana.Current, afterStart - 5f,
                "Twenty mana a second for half a second should cost about ten.");
        }

        [UnityTest]
        public IEnumerator ReleasingEndsTheChannel()
        {
            SpawnCaster();
            SpellDefinition spell = Channelled("test.channel", 5f, 10f, 0.1f, false,
                SpellTargeting.Self, World.CreatePulseEffect(MagicElement.Warding, 10f));

            spellcaster.TryCast(spell);

            yield return WaitUntil(() => spellcaster.IsChannelling, 1f, "the channel");

            spellcaster.ReleaseChannel();

            yield return WaitUntil(() => !spellcaster.IsChannelling, 1f, "the channel to stop");

            float atRelease = mana.Current;

            yield return Wait(0.4f);

            Assert.AreEqual(atRelease, mana.Current, 0.01f, "Mana must stop draining once released.");
        }

        [UnityTest]
        public IEnumerator AReleasePressedDuringTheCastTimeIsNotLost()
        {
            SpawnCaster();
            SpellDefinition spell = Channelled("test.channel", 5f, 10f, 0.1f, false,
                SpellTargeting.Self, World.CreatePulseEffect(MagicElement.Warding, 10f));
            TestFields.Set(spell, "castTime", 0.4f);

            spellcaster.TryCast(spell);
            spellcaster.ReleaseChannel();

            yield return WaitUntil(() => !spellcaster.IsCasting, 2f, "the cast to finish");

            Assert.IsFalse(spellcaster.IsChannelling,
                "A quick click must not begin a channel that cannot be stopped.");
        }

        [UnityTest]
        public IEnumerator AChannelStopsWhenTheManaRunsOut()
        {
            SpawnCaster(maxMana: 20f);
            SpellDefinition spell = Channelled("test.channel", 5f, 40f, 0.1f, false,
                SpellTargeting.Self, World.CreatePulseEffect(MagicElement.Warding, 10f));

            spellcaster.TryCast(spell);

            yield return WaitUntil(() => spellcaster.IsChannelling, 1f, "the channel");
            yield return WaitUntil(() => !spellcaster.IsChannelling, 2f, "the channel to run dry");

            // Spending is all-or-nothing: 20 - 5 up front - three 4-point ticks leaves 3.
            // The channel must stop when another whole tick is unaffordable, not drain the remainder.
            Assert.AreEqual(3f, mana.Current, 0.001f);
            Assert.Less(mana.Current, spell.ManaPerSecond * spell.ChannelTickInterval);
        }

        [UnityTest]
        public IEnumerator AChannelThatHoldsTheLockReleasesItAtTheEnd()
        {
            SpawnCaster();
            SpellDefinition spell = Channelled("test.hold", 5f, 5f, 0.1f, true,
                SpellTargeting.Self, World.CreatePulseEffect(MagicElement.Restoration, 10f));

            spellcaster.TryCast(spell);

            yield return WaitUntil(() => spellcaster.IsChannelling, 1f, "the channel");

            Assert.IsTrue(actionLock.IsLocked, "This spell is meant to pin the caster in place.");

            spellcaster.ReleaseChannel();

            yield return WaitUntil(() => !spellcaster.IsCasting, 2f, "the cast to end");

            Assert.IsFalse(actionLock.IsLocked,
                "A lock left held after a cast is a character that can never move again.");
        }

        [UnityTest]
        public IEnumerator ACooldownRefusesASecondCastAndThenExpires()
        {
            SpawnCaster();
            SpellDefinition spell = World.CreateSpell("test.bolt", SpellTargeting.Ray,
                World.CreateDamageEffect(10f));
            TestFields.Set(spell, "cooldown", 0.4f);

            spellcaster.TryCast(spell);

            yield return WaitUntil(() => !spellcaster.IsCasting, 2f, "the first cast");

            Assert.IsFalse(spellcaster.TryCast(spell), "It should still be cooling down.");

            yield return WaitUntil(() => spellcaster.CooldownRemaining(spell) <= 0f, 2f, "the cooldown");

            Assert.IsTrue(spellcaster.TryCast(spell));
        }

        [UnityTest]
        public IEnumerator SelfCastReachesEveryReceiverOnTheCaster()
        {
            SpawnCaster();

            // The regression this exists for: a self-cast used to reach only the first receiver
            // found, so which one worked depended on component order in the inspector.
            CharacterBarrier barrier = caster.AddComponent<CharacterBarrier>();
            CharacterLevitation levitation = caster.AddComponent<CharacterLevitation>();

            SpellDefinition ward = World.CreateSpell("test.ward", SpellTargeting.Self,
                World.CreatePulseEffect(MagicElement.Warding, 20f));
            SpellDefinition lift = World.CreateSpell("test.lift", SpellTargeting.Self,
                World.CreatePulseEffect(MagicElement.Force, 20f));

            spellcaster.TryCast(ward);
            yield return WaitUntil(() => !spellcaster.IsCasting, 2f, "the ward");

            Assert.IsTrue(barrier.IsUp, "The barrier is the second receiver on this object.");
            Assert.IsFalse(levitation.IsLevitating, "Warding is not Force; levitation must ignore it.");

            spellcaster.TryCast(lift);
            yield return WaitUntil(() => !spellcaster.IsCasting, 2f, "the lift");

            Assert.IsTrue(levitation.IsLevitating);
        }
    }
}
