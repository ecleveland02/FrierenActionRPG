using System;

namespace Frieren.Presentation
{
    /// <summary>Keeps brief line-of-sight losses from restarting the exploration track.</summary>
    public sealed class CombatMusicState
    {
        private float quietSeconds;
        public bool InCombat { get; private set; }

        public bool Tick(bool threatPresent, float deltaTime, float releaseDelay)
        {
            if (threatPresent)
            {
                quietSeconds = 0f;
                InCombat = true;
            }
            else
            {
                quietSeconds += Math.Max(0f, deltaTime);
                if (quietSeconds >= Math.Max(0f, releaseDelay)) InCombat = false;
            }
            return InCombat;
        }

        public void Clear() { quietSeconds = 0f; InCombat = false; }
    }
}
