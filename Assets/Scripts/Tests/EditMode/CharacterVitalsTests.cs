using Frieren.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.EditMode
{
    /// <summary>
    /// Component-level behaviour for health and mana.
    /// </summary>
    /// <remarks>
    /// These run in edit mode, where Unity does not call <c>Awake</c> or <c>Update</c>. That is
    /// deliberate rather than a limitation: the components initialise lazily, so everything except
    /// regeneration is reachable, and regeneration is the one part that genuinely needs a running
    /// clock. It belongs in a play-mode test once there is something worth driving one for.
    /// </remarks>
    public sealed class CharacterVitalsTests
    {
        private GameObject host;
        private CharacterStatsDefinition definition;

        /// <summary>Authors a definition through SerializedObject, so production code needs no test-only setters.</summary>
        private static CharacterStatsDefinition MakeDefinition(float maxHealth, float maxMana)
        {
            var created = ScriptableObject.CreateInstance<CharacterStatsDefinition>();
            var serialized = new SerializedObject(created);
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.FindProperty("maxMana").floatValue = maxMana;
            serialized.FindProperty("healthRegenPerSecond").floatValue = 0f;
            serialized.FindProperty("manaRegenPerSecond").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return created;
        }

        [SetUp]
        public void SetUp()
        {
            definition = MakeDefinition(maxHealth: 100f, maxMana: 50f);
            host = new GameObject("Character");
            host.AddComponent<CharacterStats>().SetDefinition(definition);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(definition);
        }

        private CharacterHealth Health() => host.AddComponent<CharacterHealth>();

        private CharacterMana Mana() => host.AddComponent<CharacterMana>();

        // ------------------------------------------------------------------ health

        [Test]
        public void HealthStartsFullFromTheDefinition()
        {
            CharacterHealth health = Health();

            Assert.AreEqual(100f, health.Max);
            Assert.AreEqual(100f, health.Current);
            Assert.IsTrue(health.IsAlive);
        }

        [Test]
        public void DamageReducesHealthAndReportsTheAmountTaken()
        {
            CharacterHealth health = Health();

            float taken = health.TakeDamage(new DamageInfo(30f));

            Assert.AreEqual(30f, taken, 0.001f);
            Assert.AreEqual(70f, health.Current, 0.001f);
        }

        [Test]
        public void OverkillReportsOnlyTheHealthThatWasThere()
        {
            CharacterHealth health = Health();
            health.TakeDamage(new DamageInfo(80f));

            float taken = health.TakeDamage(new DamageInfo(500f));

            Assert.AreEqual(20f, taken, 0.001f, "Damage dealt must not exceed health remaining.");
        }

        [Test]
        public void ReachingZeroRaisesDiedExactlyOnce()
        {
            CharacterHealth health = Health();
            int deaths = 0;
            health.Died += _ => deaths++;

            health.TakeDamage(new DamageInfo(100f));
            health.TakeDamage(new DamageInfo(100f));
            health.TakeDamage(new DamageInfo(100f));

            Assert.AreEqual(1, deaths, "A corpse hit again must not die again.");
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void TheDeadTakeNoFurtherDamage()
        {
            CharacterHealth health = Health();
            health.TakeDamage(new DamageInfo(100f));

            Assert.AreEqual(0f, health.TakeDamage(new DamageInfo(10f)));
        }

        [Test]
        public void TheDeadCannotBeHealed()
        {
            CharacterHealth health = Health();
            health.TakeDamage(new DamageInfo(100f));

            Assert.AreEqual(0f, health.Heal(50f), "Healing must not be an accidental resurrection.");
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void ReviveRestoresLifeAtTheRequestedFraction()
        {
            CharacterHealth health = Health();
            health.TakeDamage(new DamageInfo(100f));

            health.Revive(0.25f);

            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(25f, health.Current, 0.001f);
        }

        [Test]
        public void HealingIsCappedAtMaximum()
        {
            CharacterHealth health = Health();
            health.TakeDamage(new DamageInfo(10f));

            Assert.AreEqual(10f, health.Heal(999f), 0.001f);
            Assert.IsTrue(health.IsFull);
        }

        [Test]
        public void DamagedCarriesTheOriginalInfoAndTheAmountTaken()
        {
            CharacterHealth health = Health();
            DamageInfo received = default;
            float amount = 0f;
            health.Damaged += (info, taken) =>
            {
                received = info;
                amount = taken;
            };

            health.TakeDamage(new DamageInfo(15f, DamageType.Fire));

            Assert.AreEqual(DamageType.Fire, received.Type);
            Assert.AreEqual(15f, amount, 0.001f);
        }

        [Test]
        public void ZeroDamageChangesNothingAndRaisesNothing()
        {
            CharacterHealth health = Health();
            int changes = 0;
            health.Changed += (_, _) => changes++;

            health.TakeDamage(new DamageInfo(0f));

            Assert.AreEqual(100f, health.Current);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void RestoreStateSetsAliveExplicitlyRatherThanInferringIt()
        {
            CharacterHealth health = Health();
            health.TakeDamage(new DamageInfo(100f));

            health.RestoreState(60f, alive: true);

            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(60f, health.Current, 0.001f);
        }

        // -------------------------------------------------------------------- mana

        [Test]
        public void ManaStartsFullFromTheDefinition()
        {
            Assert.AreEqual(50f, Mana().Current);
        }

        [Test]
        public void SpendingWithinBudgetSucceeds()
        {
            CharacterMana mana = Mana();

            Assert.IsTrue(mana.TrySpend(20f));
            Assert.AreEqual(30f, mana.Current, 0.001f);
        }

        [Test]
        public void SpendingBeyondBudgetTakesNothing()
        {
            CharacterMana mana = Mana();

            Assert.IsFalse(mana.TrySpend(51f));
            Assert.AreEqual(50f, mana.Current, 0.001f, "A refused spell must not part-pay its cost.");
        }

        [Test]
        public void AFailedSpendReportsWhatItNeeded()
        {
            CharacterMana mana = Mana();
            float needed = 0f;
            mana.SpendFailed += cost => needed = cost;

            mana.TrySpend(80f);

            Assert.AreEqual(80f, needed, 0.001f);
        }

        [Test]
        public void AFreeSpellAlwaysSucceedsAndCostsNothing()
        {
            CharacterMana mana = Mana();
            mana.TrySpend(50f);

            Assert.IsTrue(mana.TrySpend(0f), "A zero-cost spell must work on an empty pool.");
            Assert.AreEqual(0f, mana.Current);
        }

        [Test]
        public void CanAffordAgreesWithTrySpend()
        {
            CharacterMana mana = Mana();

            Assert.IsTrue(mana.CanAfford(50f));
            Assert.IsFalse(mana.CanAfford(50.01f));
        }

        // ------------------------------------------------------------------- stats

        [Test]
        public void RaisingMaximumHealthKeepsTheProportion()
        {
            CharacterHealth health = Health();
            health.TakeDamage(new DamageInfo(50f));

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("maxHealth").floatValue = 200f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Called directly rather than through CharacterStats.Changed: the subscription happens
            // in OnEnable, which edit mode does not run. The event wiring is play-mode behaviour;
            // the rescaling is the part worth pinning down here.
            health.SyncMaxFromStats();

            Assert.AreEqual(200f, health.Max, 0.001f);
            Assert.AreEqual(100f, health.Current, 0.001f, "Half health before must be half health after.");
        }

        [Test]
        public void AMissingDefinitionYieldsZeroesRatherThanThrowing()
        {
            // CharacterStats logs an error for a missing definition, by design. Whether that fires
            // here depends on whether the editor ran Awake, so tolerate it either way rather than
            // asserting on a lifecycle detail this test does not care about.
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var bare = new GameObject("NoDefinition");
                bare.AddComponent<CharacterStats>();
                CharacterHealth health = bare.AddComponent<CharacterHealth>();

                Assert.AreEqual(0f, health.Max);
                Assert.AreEqual(0f, health.TakeDamage(new DamageInfo(10f)));

                Object.DestroyImmediate(bare);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
