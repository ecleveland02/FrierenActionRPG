using System;
using System.Text.RegularExpressions;
using Frieren.Save;
using Frieren.Save.Serialization;
using Frieren.Save.Storage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frieren.Tests.EditMode
{
    public sealed class SaveServiceTests
    {
        [Serializable]
        private sealed class CounterState
        {
            public int counter;
        }

        private sealed class FakeSaveable : ISaveable
        {
            private readonly CounterState state = new CounterState();

            public FakeSaveable(string saveId) => SaveId = saveId;

            public string SaveId { get; }

            public int Counter
            {
                get => state.counter;
                set => state.counter = value;
            }

            public int RestoreCallCount { get; private set; }

            public SaveEntry Capture() => SaveEntry.Create(SaveId, state);

            public void Restore(SaveEntry entry)
            {
                RestoreCallCount++;

                if (entry.TryRead(out CounterState restored))
                {
                    state.counter = restored.counter;
                }
            }
        }

        /// <summary>A saveable whose entry key does not match its SaveId - a programming error.</summary>
        private sealed class MiskeyedSaveable : ISaveable
        {
            public string SaveId => "correct.id";

            public SaveEntry Capture() => SaveEntry.Create("wrong.id", new CounterState());

            public void Restore(SaveEntry entry)
            {
            }
        }

        private InMemorySaveStorage storage;
        private SaveService service;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemorySaveStorage();
            service = new SaveService(storage);
        }

        [Test]
        public void Constructor_RequiresStorage()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveService(null));
        }

        [Test]
        public void SaveThenLoad_RestoresState()
        {
            var saveable = new FakeSaveable("player") { Counter = 5 };
            service.Register(saveable);

            Assert.IsTrue(service.Save("slot_a"));

            saveable.Counter = 99;
            Assert.IsTrue(service.Load("slot_a"));

            Assert.AreEqual(5, saveable.Counter);
        }

        [Test]
        public void Load_ReturnsFalseForMissingSlot()
        {
            Assert.IsFalse(service.Load("nope"));
        }

        [Test]
        public void Register_AfterLoad_ImmediatelyRestoresTheLateSaveable()
        {
            // The case that matters in game: a scene is still streaming in when the file loads.
            var early = new FakeSaveable("player") { Counter = 3 };
            service.Register(early);
            service.Save("slot_a");

            var freshService = new SaveService(storage);
            Assert.IsTrue(freshService.Load("slot_a"));

            var late = new FakeSaveable("player");
            freshService.Register(late);

            Assert.AreEqual(3, late.Counter);
            Assert.AreEqual(1, late.RestoreCallCount);
        }

        [Test]
        public void Register_DoesNotRestoreSaveablesAbsentFromTheFile()
        {
            service.Register(new FakeSaveable("player"));
            service.Save("slot_a");
            service.Load("slot_a");

            var newcomer = new FakeSaveable("enemy.spawner");
            service.Register(newcomer);

            Assert.AreEqual(0, newcomer.RestoreCallCount);
        }

        [Test]
        public void Register_RejectsDuplicateSaveIds()
        {
            service.Register(new FakeSaveable("player"));

            Assert.Throws<InvalidOperationException>(() => service.Register(new FakeSaveable("player")));
        }

        [Test]
        public void Register_IsIdempotentForTheSameInstance()
        {
            var saveable = new FakeSaveable("player");
            service.Register(saveable);

            Assert.DoesNotThrow(() => service.Register(saveable));
            Assert.AreEqual(1, service.RegisteredIds.Count);
        }

        [Test]
        public void Register_RejectsEmptySaveId()
        {
            Assert.Throws<ArgumentException>(() => service.Register(new FakeSaveable(string.Empty)));
        }

        [Test]
        public void Unregister_StopsTheSaveableContributingState()
        {
            var saveable = new FakeSaveable("player") { Counter = 4 };
            service.Register(saveable);

            Assert.IsTrue(service.Unregister(saveable));
            service.Save("slot_a");

            Assert.IsFalse(service.Current.TryGet("player", out _));
        }

        [Test]
        public void MiskeyedEntry_IsRejectedRatherThanWrittenUnderTheWrongKey()
        {
            service.Register(new MiskeyedSaveable());
            LogAssert.Expect(LogType.Error, new Regex("produced an entry keyed"));

            service.Save("slot_a");

            Assert.IsFalse(service.Current.TryGet("wrong.id", out _));
            Assert.IsFalse(service.Current.TryGet("correct.id", out _));
        }

        [Test]
        public void Delete_RemovesTheSlot()
        {
            service.Register(new FakeSaveable("player"));
            service.Save("slot_a");

            Assert.IsTrue(service.HasSave("slot_a"));
            Assert.IsTrue(service.Delete("slot_a"));
            Assert.IsFalse(service.HasSave("slot_a"));
            Assert.IsFalse(service.Delete("slot_a"));
        }

        [Test]
        public void ListSlots_ReportsWrittenSlots()
        {
            service.Register(new FakeSaveable("player"));
            service.Save("slot_b");
            service.Save("slot_a");

            CollectionAssert.AreEqual(new[] { "slot_a", "slot_b" }, service.ListSlots());
        }

        [Test]
        public void Save_RaisesSaveWritten()
        {
            string written = null;
            service.SaveWritten += slot => written = slot;

            service.Save("slot_a");

            Assert.AreEqual("slot_a", written);
        }

        [Test]
        public void Load_RejectsAFileFromANewerBuild()
        {
            var future = new SaveGameData { Version = SaveGameData.CurrentVersion + 1 };
            storage.Write("slot_future", JsonUtility.ToJson(future));

            LogAssert.Expect(LogType.Error, new Regex("newer than this build supports"));

            Assert.IsFalse(service.Load("slot_future"));
        }
    }
}
