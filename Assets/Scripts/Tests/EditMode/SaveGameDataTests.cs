using System;
using Frieren.Save.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace Frieren.Tests.EditMode
{
    public sealed class SaveGameDataTests
    {
        [Serializable]
        private sealed class SampleState
        {
            public int value;
        }

        [Test]
        public void Put_ReplacesAnEntryWithTheSameKey()
        {
            var data = new SaveGameData();
            data.Put(SaveEntry.Create("a", new SampleState { value = 1 }));
            data.Put(SaveEntry.Create("a", new SampleState { value = 2 }));

            Assert.AreEqual(1, data.Entries.Count);
            Assert.IsTrue(data.TryGet("a", out SaveEntry entry));
            Assert.IsTrue(entry.TryRead(out SampleState state));
            Assert.AreEqual(2, state.value);
        }

        [Test]
        public void TryGet_ReturnsFalseForUnknownKey()
        {
            var data = new SaveGameData();

            Assert.IsFalse(data.TryGet("missing", out SaveEntry entry));
            Assert.IsNull(entry);
        }

        [Test]
        public void Remove_DropsTheEntry()
        {
            var data = new SaveGameData();
            data.Put(SaveEntry.Create("a", new SampleState()));

            Assert.IsTrue(data.Remove("a"));
            Assert.IsFalse(data.Remove("a"));
            Assert.AreEqual(0, data.Entries.Count);
        }

        [Test]
        public void SurvivesJsonUtilityRoundTrip()
        {
            var data = new SaveGameData { SceneId = "ruins", PlayTimeSeconds = 123.5 };
            data.Put(SaveEntry.Create("a", new SampleState { value = 9 }));
            data.MarkSaved();

            var restored = JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data));

            Assert.AreEqual(SaveGameData.CurrentVersion, restored.Version);
            Assert.AreEqual("ruins", restored.SceneId);
            Assert.AreEqual(123.5, restored.PlayTimeSeconds, 0.0001);
            Assert.IsTrue(restored.TryGet("a", out SaveEntry entry));
            Assert.IsTrue(entry.TryRead(out SampleState state));
            Assert.AreEqual(9, state.value);
        }

        [Test]
        public void Migration_RejectsAFileFromANewerBuild()
        {
            var data = new SaveGameData { Version = SaveGameData.CurrentVersion + 1 };

            Assert.IsFalse(SaveMigration.TryMigrate(data, out string reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Migration_AcceptsACurrentFile()
        {
            Assert.IsTrue(SaveMigration.TryMigrate(new SaveGameData(), out string reason));
            Assert.IsNull(reason);
        }
    }
}
