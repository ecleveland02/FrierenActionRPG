using Frieren.Presentation;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class CombatMusicStateTests
    {
        [Test]
        public void ExplorationIsTheDefault() => Assert.IsFalse(new CombatMusicState().InCombat);

        [Test]
        public void ThreatStartsCombatAndQuietMustLastThroughTheHold()
        {
            var state = new CombatMusicState();
            Assert.IsTrue(state.Tick(true, 0f, 4f));
            Assert.IsTrue(state.Tick(false, 3f, 4f));
            Assert.IsFalse(state.Tick(false, 1f, 4f));
        }

        [Test]
        public void RenewedThreatRestartsTheQuietTimer()
        {
            var state = new CombatMusicState();
            state.Tick(true, 0f, 4f);
            state.Tick(false, 3f, 4f);
            state.Tick(true, 1f, 4f);
            Assert.IsTrue(state.Tick(false, 3f, 4f));
            Assert.IsFalse(state.Tick(false, 1f, 4f));
        }

        [Test]
        public void PauseDoesNotConsumeTheHoldAndClearReturnsToExploration()
        {
            var state = new CombatMusicState();
            state.Tick(true, 0f, 4f);
            Assert.IsTrue(state.Tick(false, 0f, 4f));
            state.Clear();
            Assert.IsFalse(state.InCombat);
        }
    }
}
