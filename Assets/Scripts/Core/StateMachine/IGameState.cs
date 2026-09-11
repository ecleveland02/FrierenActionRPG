namespace Frieren.Core.StateMachine
{
    public interface IGameState
    {
        void Enter();

        void Exit();

        void Tick(float deltaTime);
    }
}
