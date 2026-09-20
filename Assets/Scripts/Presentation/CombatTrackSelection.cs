namespace Frieren.Presentation
{
    public static class CombatTrackSelection
    {
        public static int Choose(int count, int previous, int randomValue)
        {
            if (count <= 1) return 0;
            int candidate = randomValue % count;
            if (candidate < 0) candidate += count;
            return candidate == previous ? (candidate + 1) % count : candidate;
        }
    }
}
