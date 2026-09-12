using System.Collections;
using Frieren.Characters;
using Frieren.Core;
using Frieren.Core.Magic;
using Frieren.Core.Persistence;
using Frieren.Core.Services;
using Frieren.Save;
using Frieren.Save.Storage;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Save and load, end to end: change the world, write it, change it back, read it, check.
    /// </summary>
    /// <remarks>
    /// A round trip is the only assertion worth making about persistence. Checking that
    /// <c>CaptureState</c> returns the right JSON tests the serialiser; checking that a burnt crate
    /// is still burnt after a load tests the thing anyone actually cares about.
    ///
    /// Storage is in memory, so these never touch the disk and cannot leave a save file behind for
    /// the next run to find.
    /// </remarks>
    public sealed class PersistencePlayModeTests : PlayModeTestBase
    {
        private SaveService saves;

        [SetUp]
        public void CreateSaveService()
        {
            saves = new SaveService(new InMemorySaveStorage());
            saves.NewGame();
            ServiceLocator.Register(saves);
        }

        /// <summary>
        /// Builds the object inactive so every <c>Awake</c> sees a finished object, then activates
        /// it and lets one frame pass so <c>PersistentObject.Start</c> can register.
        /// </summary>
        private IEnumerator Persistent<T>(string id, System.Action<T> configure, System.Action<T> ready)
            where T : Component
        {
            var host = new GameObject(id);
            host.SetActive(false);
            World.Track(host);

            host.AddComponent<SceneObjectId>().Assign(id);
            T component = host.AddComponent<T>();
            configure?.Invoke(component);
            host.AddComponent<PersistentObject>();

            host.SetActive(true);
            yield return null;

            ready(component);
        }

        [UnityTest]
        public IEnumerator AnUnlockedDoorStaysUnlocked()
        {
            LockedObject door = null;
            yield return Persistent<LockedObject>("test.door", null, d => door = d);

            door.Unlock(null);
            Assert.IsTrue(door.IsOpen);
            Assert.IsTrue(saves.Save());

            // Put the world back the way it was, so a passing test cannot be a test that did
            // nothing. Written out as the state's own JSON rather than by poking private fields,
            // which exercises the restore path in both directions.
            door.RestoreState("{\"locked\":true,\"open\":false,\"pickProgress\":0}");
            Assert.IsTrue(door.IsLocked, "The reset must actually have reset it.");
            yield return null;

            Assert.IsTrue(saves.Load());

            Assert.IsFalse(door.IsLocked, "A door opened before the save must not be locked after the load.");
            Assert.IsTrue(door.IsOpen);
        }

        [UnityTest]
        public IEnumerator AHalfPickedLockStaysHalfPicked()
        {
            LockedObject door = null;
            yield return Persistent<LockedObject>("test.door", null, d => door = d);

            door.ReceiveMagic(new MagicPulse(MagicElement.Unbinding, 10f, Vector3.zero));
            Assert.IsTrue(door.IsLocked, "Ten is not enough to pick a fifteen lock.");
            saves.Save();

            door.ReceiveMagic(new MagicPulse(MagicElement.Unbinding, 10f, Vector3.zero));
            Assert.IsFalse(door.IsLocked);

            saves.Load();

            Assert.IsTrue(door.IsLocked, "The load should have put the half-picked lock back.");

            // And the progress came with it: one more small pulse finishes the job.
            door.ReceiveMagic(new MagicPulse(MagicElement.Unbinding, 6f, Vector3.zero));
            Assert.IsFalse(door.IsLocked, "Restoring the progress is the point, not just the locked flag.");
        }

        [UnityTest]
        public IEnumerator AFrozenTroughStaysFrozen()
        {
            WaterBasin basin = null;
            yield return Persistent<WaterBasin>("test.trough", null, b => basin = b);

            basin.ReceiveMagic(new MagicPulse(MagicElement.Water, 30f, Vector3.zero));
            basin.ReceiveMagic(new MagicPulse(MagicElement.Cold, 30f, Vector3.zero));
            Assert.AreEqual(BasinState.Frozen, basin.State);

            saves.Save();

            basin.ReceiveMagic(new MagicPulse(MagicElement.Heat, 30f, Vector3.zero));
            basin.ReceiveMagic(new MagicPulse(MagicElement.Heat, 30f, Vector3.zero));
            Assert.AreEqual(BasinState.Empty, basin.State);

            saves.Load();

            Assert.AreEqual(BasinState.Frozen, basin.State,
                "An ice bridge you built and saved must still be there when you come back.");
        }

        [UnityTest]
        public IEnumerator AMendedPillarStaysMended()
        {
            RepairableObject pillar = null;
            yield return Persistent<RepairableObject>("test.pillar", null, p => pillar = p);

            pillar.ReceiveMagic(new MagicPulse(MagicElement.Restoration, 100f, Vector3.zero));
            Assert.IsTrue(pillar.IsIntact);

            saves.Save();
            pillar.Break();
            Assert.IsFalse(pillar.IsIntact);

            saves.Load();

            Assert.IsTrue(pillar.IsIntact);
        }

        [UnityTest]
        public IEnumerator ABurntCrateStaysBurnt()
        {
            FlammableObject crate = null;
            yield return Persistent<FlammableObject>("test.crate",
                c =>
                {
                    TestFields.Set(c, "ignitionThreshold", 10f);
                    TestFields.Set(c, "burnDuration", 0.2f);
                    TestFields.Set(c, "disableWhenConsumed", false);
                },
                c => crate = c);

            crate.ReceiveMagic(new MagicPulse(MagicElement.Heat, 20f, Vector3.zero));
            yield return WaitUntil(() => crate.IsConsumed, 3f, "the crate to burn away");

            saves.Save();
            saves.Load();

            Assert.IsTrue(crate.IsConsumed, "Burning something down should not be undone by a reload.");
        }

        [UnityTest]
        public IEnumerator ACharacterKeepsItsVitalsAndItsPosition()
        {
            GameObject character = World.CreateCharacter("Hero", new Vector3(0f, 0.1f, 0f),
                GameLayers.Player, World.CreateStats(maxHealth: 100f, maxMana: 100f), activate: false);
            character.AddComponent<SceneObjectId>().Assign("test.hero");
            character.AddComponent<CharacterPersistence>();
            character.AddComponent<PersistentObject>();
            character.SetActive(true);
            yield return null;

            CharacterHealth health = character.GetComponent<CharacterHealth>();
            CharacterMana mana = character.GetComponent<CharacterMana>();
            CharacterMotor motor = character.GetComponent<CharacterMotor>();

            health.TakeDamage(new DamageInfo(35f));
            mana.TrySpend(20f);
            motor.Teleport(new Vector3(6f, 0.1f, -4f), Quaternion.identity);
            yield return null;

            saves.Save();

            health.Heal(100f);
            mana.Restore(100f);
            motor.Teleport(new Vector3(-9f, 0.1f, 12f), Quaternion.identity);
            yield return null;

            saves.Load();
            yield return null;

            Assert.AreEqual(65f, health.Current, 0.5f);
            Assert.AreEqual(80f, mana.Current, 0.5f);
            Assert.AreEqual(6f, character.transform.position.x, 0.3f,
                "Loading should put the character back where it was, not leave it where it is.");
            Assert.AreEqual(-4f, character.transform.position.z, 0.3f);
        }

        [UnityTest]
        public IEnumerator AnObjectWithNoSaveInTheFileKeepsItsCurrentState()
        {
            // Written before the door exists, so the file holds no entry for it. Loading must leave
            // the door alone rather than resetting it - otherwise every object added to the game
            // after a player's last save would wipe itself the moment they loaded.
            saves.Save();

            LockedObject door = null;
            yield return Persistent<LockedObject>("test.door", null, d => door = d);

            door.Unlock(null);
            saves.Load();

            Assert.IsTrue(door.IsOpen, "An entry that was never written must not overwrite anything.");
        }
    }
}
