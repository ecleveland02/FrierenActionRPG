using System;

namespace Frieren.Core.StateMachine
{
    /// <summary>
    /// A state defined by callbacks, for states too small to deserve their own class.
    /// </summary>
    public sealed class DelegateGameState : IGameState
    {
        private readonly Action onEnter;
        private readonly Action onExit;
        private readonly Action<float> onTick;

        public DelegateGameState(Action onEnter = null, Action onExit = null, Action<float> onTick = null)
        {
            this.onEnter = onEnter;
            this.onExit = onExit;
            this.onTick = onTick;
        }

        public void Enter() => onEnter?.Invoke();

        public void Exit() => onExit?.Invoke();

        public void Tick(float deltaTime) => onTick?.Invoke(deltaTime);
    }
}
