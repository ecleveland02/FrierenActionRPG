using System;
using Frieren.Save.Serialization;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class SaveEntryTests
    {
        [Serializable]
        private sealed class SampleState
        {
            public int health;
            public string sceneId;
        }

        [Serializable]
        private sealed class UnrelatedState
        {
            public float mana;
        }

        [Test]
        public void Create_RoundTripsThroughTryRead()
        {
            SaveEntry entry = SaveEntry.Create("player.health", new SampleState { health = 42, sceneId = "forest" });

            Assert.IsTrue(entry.TryRead(out SampleState restored));
            Assert.AreEqual(42, restored.health);
            Assert.AreEqual("forest", restored.sceneId);
            Assert.AreEqual("player.health", entry.Key);
        }

        [Test]
        public void Create_RejectsEmptyKey()
        {
            Assert.Throws<ArgumentException>(() => SaveEntry.Create(string.Empty, new SampleState()));
        }

        [Test]
        public void Create_RejectsNullState()
        {
            Assert.Throws<ArgumentNullException>(() => SaveEntry.Create<SampleState>("key", null));
        }

        [Test]
        public void TryRead_WithMismatchedType_YieldsDefaultsRatherThanThrowing()
        {
            // JsonUtility ignores unknown fields, so reading as the wrong type is silent data loss
            // rather than an exception. Documented here so the behaviour is a known quantity.
            SaveEntry entry = SaveEntry.Create("k", new SampleState { health = 42 });

            Assert.IsTrue(entry.TryRead(out UnrelatedState restored));
            Assert.AreEqual(0f, restored.mana);
        }

        [Test]
        public void TypeName_RecordsTheSourceTypeForDiagnostics()
        {
            SaveEntry entry = SaveEntry.Create("k", new SampleState());

            Assert.IsTrue(entry.TypeName.EndsWith("SampleState"), entry.TypeName);
        }
    }
}
