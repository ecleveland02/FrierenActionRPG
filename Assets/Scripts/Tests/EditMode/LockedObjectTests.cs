using Frieren.Core.Magic;
using Frieren.World;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    public sealed class LockedObjectTests
    {
        private const float Unbinding = 15f;
        private const float Force = 40f;

        private GameObject host;
        private LockedObject door;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Door");
            door = host.AddComponent<LockedObject>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        private bool Send(MagicElement element, float magnitude) =>
            door.ReceiveMagic(new MagicPulse(element, magnitude, Vector3.zero));

        [Test]
        public void StartsLockedAndShut()
        {
            Assert.IsTrue(door.IsLocked);
            Assert.IsFalse(door.IsOpen);
        }

        [Test]
        public void UnbindingAccumulatesUntilThePickSucceeds()
        {
            Assert.IsTrue(Send(MagicElement.Unbinding, Unbinding - 1f));
            Assert.IsTrue(door.IsLocked, "Partial progress is progress, not an unlock.");

            Assert.IsTrue(Send(MagicElement.Unbinding, 1f));
            Assert.IsFalse(door.IsLocked);
            Assert.IsTrue(door.IsOpen, "openOnUnlock defaults on, so a picked lock swings open.");
        }

        [Test]
        public void WeakForceBouncesOff()
        {
            Assert.IsFalse(Send(MagicElement.Force, Force - 1f));
            Assert.IsTrue(door.IsLocked);
        }

        [Test]
        public void EnoughForceBreaksItInOneGo()
        {
            Assert.IsTrue(Send(MagicElement.Force, Force));
            Assert.IsFalse(door.IsLocked);
        }

        [Test]
        public void ForceDoesNotAccumulate()
        {
            Send(MagicElement.Force, Force * 0.6f);

            Assert.IsFalse(Send(MagicElement.Force, Force * 0.6f),
                "Shoving is all or nothing; two weak shoves must not add up to one strong one.");
            Assert.IsTrue(door.IsLocked);
        }

        [Test]
        public void InteractingWhileLockedDoesNotOpenIt()
        {
            door.Interact(null);

            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual("Locked", door.InteractionPrompt);
        }

        [Test]
        public void AKeyOpensItThroughTheSamePathAsMagic()
        {
            Assert.IsTrue(door.Unlock(null));
            Assert.IsTrue(door.IsOpen);
            Assert.IsFalse(door.Unlock(null), "Unlocking twice must not report a second success.");
        }

        [Test]
        public void AnOpenDoorStopsCaringAboutMagic()
        {
            door.Unlock(null);

            Assert.IsFalse(Send(MagicElement.Unbinding, 100f));
            Assert.IsFalse(Send(MagicElement.Force, 100f));
        }
    }
}
