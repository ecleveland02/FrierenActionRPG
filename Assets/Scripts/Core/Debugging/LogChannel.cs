using System;

namespace Frieren.Core.Debugging
{
    /// <summary>
    /// Subsystem tags for log lines, so a noisy system can be silenced without silencing the rest.
    /// </summary>
    [Flags]
    public enum LogChannel
    {
        None = 0,
        Core = 1 << 0,
        Scenes = 1 << 1,
        Save = 1 << 2,
        Input = 1 << 3,
        Player = 1 << 4,
        Combat = 1 << 5,
        Magic = 1 << 6,
        AI = 1 << 7,
        Interaction = 1 << 8,
        UI = 1 << 9,
        Audio = 1 << 10,
        All = ~0
    }
}
