namespace Frieren.Core.Debugging
{
    /// <summary>
    /// A human-readable marker for which revision the editor is actually running.
    /// </summary>
    /// <remarks>
    /// Bumped by hand on every commit that changes behaviour. It exists because "did the pull land
    /// and did Unity recompile" is invisible from inside the running game, and several hours were
    /// spent debugging code that was never on the machine. Git can say one thing while the editor
    /// runs another - a stale pull, a pull into an open editor, a recompile that never happened -
    /// and the only reliable signal is one the game itself prints.
    ///
    /// If the overlay does not show the value you were told to expect, nothing else observed in
    /// that session means anything.
    /// </remarks>
    public static class BuildStamp
    {
        /// <summary>Bump this whenever behaviour changes. Shown in the debug overlay.</summary>
        public const string Current = "m7.2 boot-watchtower, look input, monster art";
    }
}
