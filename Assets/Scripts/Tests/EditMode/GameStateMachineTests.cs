using System;
using System.Collections.Generic;
using Frieren.Core.StateMachine;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class GameStateMachineTests
    {
        private sealed class RecordingState : IGameState
        {
            private readonly string label;
            private readonly List<string> log;

            public RecordingState(string label, List<string> log)
            {
                this.label = label;
                this.log = log;
            }

            public int TickCount { get; private set; }

            public void Enter() => log.Add($"enter:{label}");

            public void Exit() => log.Add($"exit:{label}");

            public void Tick(float deltaTime) => TickCount++;
        }

        private List<string> log;
        private GameStateMachine machine;
        private RecordingState playing;
        private RecordingState paused;

        [SetUp]
        public void SetUp()
        {
            log = new List<string>();
            machine = new GameStateMachine();
            playing = new RecordingState("playing", log);
            paused = new RecordingState("paused", log);
            machine.Register(GameStateId.Playing, playing);
            machine.Register(GameStateId.Paused, paused);
        }

        [Test]
        public void ChangeTo_UnregisteredState_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => machine.ChangeTo(GameStateId.MainMenu));
        }

        [Test]
        public void ChangeTo_ExitsPreviousBeforeEnteringNext()
        {
            machine.ChangeTo(GameStateId.Playing);
            machine.ChangeTo(GameStateId.Paused);

            CollectionAssert.AreEqual(new[] { "enter:playing", "exit:playing", "enter:paused" }, log);
            Assert.AreEqual(GameStateId.Paused, machine.Current);
        }

        [Test]
        public void ChangeTo_SameStateTwice_DoesNotReEnter()
        {
            machine.ChangeTo(GameStateId.Playing);
            machine.ChangeTo(GameStateId.Playing);

            CollectionAssert.AreEqual(new[] { "enter:playing" }, log);
        }

        [Test]
        public void StateChanged_ReportsPreviousAndCurrent()
        {
            GameStateId? from = null;
            GameStateId? to = null;
            machine.StateChanged += (previous, current) =>
            {
                from = previous;
                to = current;
            };

            machine.ChangeTo(GameStateId.Playing);
            machine.ChangeTo(GameStateId.Paused);

            Assert.AreEqual(GameStateId.Playing, from);
            Assert.AreEqual(GameStateId.Paused, to);
        }

        [Test]
        public void Tick_OnlyReachesTheCurrentState()
        {
            machine.ChangeTo(GameStateId.Playing);
            machine.Tick(0.016f);
            machine.ChangeTo(GameStateId.Paused);
            machine.Tick(0.016f);
            machine.Tick(0.016f);

            Assert.AreEqual(1, playing.TickCount);
            Assert.AreEqual(2, paused.TickCount);
        }
    }
}
