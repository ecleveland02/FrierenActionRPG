namespace Frieren.Core.StateMachine
{
    /// <summary>
    /// High-level modes the game can be in. Systems gate their behaviour on this rather than on
    /// ad-hoc booleans scattered across controllers.
    /// </summary>
    public enum GameStateId
    {
        Booting = 0,
        MainMenu = 1,
        Loading = 2,
        Playing = 3,
        Paused = 4
    }
}
