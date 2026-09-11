using System;
using System.Collections.Generic;

namespace Frieren.Core.StateMachine
{
    /// <summary>
    /// Flat state machine over <see cref="GameStateId"/>.
    /// </summary>
    /// <remarks>
    /// Intentionally flat and free of transition rules. The states here are coarse application
    /// modes, not character behaviour; enemy AI in Milestone 5 gets its own behaviour state
    /// machine rather than being crammed into this one.
    /// </remarks>
    public sealed class GameStateMachine
    {
        private readonly Dictionary<GameStateId, IGameState> states = new Dictionary<GameStateId, IGameState>();

        private IGameState currentState;

        public GameStateId Current { get; private set; } = GameStateId.Booting;

        public bool HasEntered { get; private set; }

        /// <summary>Raised after the transition completes, as (previous, current).</summary>
        public event Action<GameStateId, GameStateId> StateChanged;

        public void Register(GameStateId id, IGameState state)
        {
            states[id] = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool IsRegistered(GameStateId id) => states.ContainsKey(id);

        public void ChangeTo(GameStateId id)
        {
            if (!states.TryGetValue(id, out IGameState next))
            {
                throw new InvalidOperationException($"Game state '{id}' has not been registered.");
            }

            if (HasEntered && Current == id)
            {
                return;
            }

            GameStateId previous = Current;

            currentState?.Exit();
            currentState = next;
            Current = id;
            HasEntered = true;
            currentState.Enter();

            StateChanged?.Invoke(previous, id);
        }

        public void Tick(float deltaTime) => currentState?.Tick(deltaTime);
    }
}
