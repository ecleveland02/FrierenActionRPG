namespace Frieren.Player
{
    public static class DodgeTiming
    {
        public static bool IsProtected(float elapsed, float start, float duration) =>
            duration > 0f && elapsed >= start && elapsed < start + duration - 0.0001f;
    }
}
