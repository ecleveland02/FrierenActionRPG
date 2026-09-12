using System.Collections;
using System.Collections.Generic;
using Frieren.Characters;
using Frieren.Core;
using Frieren.Core.Magic;
using Frieren.Magic;
using Frieren.Player;
using Frieren.Player.Cameras;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// The block button and the spell wheel, driven through their public API rather than through
    /// real devices - the Input System's own test fixtures are a separate problem, and what matters
    /// here is what happens after the button is read, not that the button is read.
    /// </summary>
    public sealed class BlockAndWheelPlayModeTests : PlayModeTestBase
    {
        private GameObject player;
        private CharacterSpellcaster spellcaster;
        private CharacterBarrier barrier;
        private CharacterHealth health;
        private PlayerSpellInput spells;
        private SpellDefinition blockSpell;
        private readonly List<SpellDefinition> known = new List<SpellDefinition>();

        private PlayerBlockInput block;
        private SpellWheelInput wheel;

        /// <summary>
        /// Builds the whole player inactive and activates it once.
        /// </summary>
        /// <remarks>
        /// Every one of these input components logs an error in <c>Awake</c> when its reader or its
        /// spell is missing, and a logged error fails a Unity test. Adding them to a live object
        /// would collapse the fixture before a single assertion ran.
        /// </remarks>
        private void SpawnPlayer(int spellCount = 4, bool withBlock = true, bool withWheel = true)
        {
            player = World.CreateCharacter("Player", new Vector3(0f, 0.1f, 0f), GameLayers.Player,
                World.CreateStats(maxMana: 200f, manaRegenPerSecond: 0f), activate: false);

            Core.Input.InputReader reader = World.CreateSilentInputReader();

            spellcaster = player.AddComponent<CharacterSpellcaster>();
            barrier = player.AddComponent<CharacterBarrier>();

            known.Clear();

            for (int i = 0; i < spellCount; i++)
            {
                known.Add(World.CreateSpell($"test.spell_{i}", SpellTargeting.Ray,
                    World.CreateDamageEffect(5f)));
            }

            spells = player.AddComponent<PlayerSpellInput>();
            TestFields.Set(spells, "inputReader", reader);
            TestFields.Set(spells, "knownSpells", new List<SpellDefinition>(known));

            blockSpell = World.CreateSpell("test.block", SpellTargeting.Self,
                World.CreatePulseEffect(MagicElement.Warding, 20f));
            TestFields.Set(blockSpell, "castMode", SpellCastMode.Channelled);
            TestFields.Set(blockSpell, "manaPerSecond", 10f);
            TestFields.Set(blockSpell, "channelTickInterval", 0.1f);
            TestFields.Set(blockSpell, "holdsActionLock", false);
            TestFields.Set(blockSpell, "castTime", 0.05f);

            block = null;
            wheel = null;

            if (withBlock)
            {
                block = player.AddComponent<PlayerBlockInput>();
                TestFields.Set(block, "inputReader", reader);
                TestFields.Set(block, "blockSpell", blockSpell);
            }

            if (withWheel)
            {
                wheel = player.AddComponent<SpellWheelInput>();
                TestFields.Set(wheel, "inputReader", reader);
            }

            player.SetActive(true);
            health = player.GetComponent<CharacterHealth>();
            health.CacheModifiers();
        }

        // ------------------------------------------------------------------------ block

        [UnityTest]
        public IEnumerator BlockingRaisesAWardAndReleasingDropsIt()
        {
            SpawnPlayer();

            spellcaster.TryCast(blockSpell);
            yield return WaitUntil(() => barrier.IsUp, 2f, "the ward to go up");

            Assert.IsTrue(block.IsBlocking, "The block input should recognise its own spell running.");

            spellcaster.ReleaseChannel(blockSpell);
            yield return WaitUntil(() => !barrier.IsUp, 2f, "the ward to lapse after release");
        }

        [UnityTest]
        public IEnumerator AWardRaisedByBlockingAbsorbsDamage()
        {
            SpawnPlayer();

            spellcaster.TryCast(blockSpell);
            yield return WaitUntil(() => barrier.IsUp, 2f, "the ward");

            health.TakeDamage(new DamageInfo(30f));

            Assert.AreEqual(health.Max, health.Current, 0.01f, "The block should have eaten that.");
        }

        [UnityTest]
        public IEnumerator CastingIsRefusedWhileBlocking()
        {
            SpawnPlayer();

            spellcaster.TryCast(blockSpell);
            yield return WaitUntil(() => spellcaster.IsChannelling, 2f, "the block");

            Assert.IsFalse(spellcaster.TryCast(known[0]),
                "One spell at a time: blocking occupies the caster, and that is the intended rule.");
            Assert.AreSame(blockSpell, spellcaster.CurrentSpell);
        }

        [UnityTest]
        public IEnumerator ReleasingTheCastButtonDoesNotDropTheBlock()
        {
            SpawnPlayer();

            spellcaster.TryCast(blockSpell);
            yield return WaitUntil(() => spellcaster.IsChannelling, 2f, "the block");

            // The regression this scoping exists for. A refused cast still releases on button-up,
            // and an unscoped release would land on the ward instead.
            spellcaster.TryCast(known[0]);
            spellcaster.ReleaseChannel(known[0]);

            yield return Wait(0.3f);

            Assert.IsTrue(spellcaster.IsChannelling, "Tapping cast must not cancel a held block.");
            Assert.IsTrue(block.IsBlocking);
            Assert.IsTrue(barrier.IsUp);
        }

        [UnityTest]
        public IEnumerator BlockingDoesNotPinTheCharacterInPlace()
        {
            SpawnPlayer();

            spellcaster.TryCast(blockSpell);
            yield return WaitUntil(() => spellcaster.IsChannelling, 2f, "the block");

            Assert.IsFalse(player.GetComponent<CharacterActionLock>().IsLocked,
                "A block you cannot move behind is a worse block.");
        }

        // ------------------------------------------------------------------------ wheel

        [UnityTest]
        public IEnumerator TheWheelOpensOnTheCurrentSelectionAndCommitsOnRelease()
        {
            SpawnPlayer(spellCount: 4);
            yield return null;

            spells.Select(2);
            wheel.Open();

            Assert.IsTrue(wheel.IsOpen);
            Assert.AreEqual(2, wheel.Highlighted, "It should open on what is already selected.");

            wheel.CloseAndCommit();

            Assert.IsFalse(wheel.IsOpen);
            Assert.AreEqual(2, spells.SelectedIndex, "Opening and closing without moving changes nothing.");
        }

        [UnityTest]
        public IEnumerator ANumberPressedWhileTheWheelIsOpenMovesTheHighlight()
        {
            SpawnPlayer(spellCount: 4);
            yield return null;

            wheel.Open();

            // PlayerSpellInput reads the number row itself; the wheel only notices the result move.
            spells.Select(3);
            yield return null;

            Assert.AreEqual(3, wheel.Highlighted);

            wheel.CloseAndCommit();
            Assert.AreEqual(3, spells.SelectedIndex);
        }

        [UnityTest]
        public IEnumerator ClosingWithoutCommittingLeavesTheSelectionAlone()
        {
            SpawnPlayer(spellCount: 4);
            yield return null;

            spells.Select(1);
            wheel.Open();
            wheel.Close();

            Assert.AreEqual(1, spells.SelectedIndex);
            Assert.IsFalse(wheel.IsOpen);
        }

        [UnityTest]
        public IEnumerator TheWheelTakesThePointerFromTheCameraAndGivesItBack()
        {
            SpawnPlayer(spellCount: 4);

            var rigHost = new GameObject("CameraRig");
            World.Track(rigHost);
            OrbitCameraRig rig = rigHost.AddComponent<OrbitCameraRig>();
            wheel.SetCameraRig(rig);
            yield return null;

            Assert.IsTrue(rig.LookEnabled);

            wheel.Open();
            Assert.IsFalse(rig.LookEnabled, "Aiming the wheel and turning the camera cannot share a mouse.");

            wheel.CloseAndCommit();
            Assert.IsTrue(rig.LookEnabled, "Leaving the camera switched off would be unrecoverable.");
        }

        [UnityTest]
        public IEnumerator DisablingTheWheelMidOpenRestoresTheCamera()
        {
            SpawnPlayer(spellCount: 4);

            var rigHost = new GameObject("CameraRig");
            World.Track(rigHost);
            OrbitCameraRig rig = rigHost.AddComponent<OrbitCameraRig>();
            wheel.SetCameraRig(rig);
            yield return null;

            wheel.Open();
            wheel.enabled = false;
            yield return null;

            Assert.IsTrue(rig.LookEnabled, "A component switched off mid-wheel must not strand the camera.");
        }

        [UnityTest]
        public IEnumerator AnEmptyWheelDoesNotOpen()
        {
            SpawnPlayer(spellCount: 0);
            yield return null;

            wheel.Open();

            Assert.IsFalse(wheel.IsOpen, "There is nothing to choose from.");
        }
    }
}
