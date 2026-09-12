using System.Collections.Generic;

namespace Frieren.Magic
{
    /// <summary>
    /// Remembers when each spell may next be cast.
    /// </summary>
    /// <remarks>
    /// Keyed by the spell's stable id rather than by asset reference, so a cooldown survives a
    /// definition being reloaded and can be written into a save file later without holding an
    /// object reference that means nothing on disk.
    ///
    /// Times are passed in rather than read from <c>Time.time</c>, for the same reason as
    /// <c>JumpGate</c>: cooldown behaviour is entirely about timing, which is miserable to confirm
    /// by feel and trivial to confirm in a test.
    /// </remarks>
    public sealed class SpellCooldownTracker
    {
        private readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();

        public bool IsReady(string spellId, float now)
        {
            if (string.IsNullOrEmpty(spellId))
            {
                return false;
            }

            return !readyAt.TryGetValue(spellId, out float ready) || now >= ready;
        }

        /// <summary>Seconds until the spell is castable again, or zero if it already is.</summary>
        public float RemainingFor(string spellId, float now)
        {
            if (string.IsNullOrEmpty(spellId) || !readyAt.TryGetValue(spellId, out float ready))
            {
                return 0f;
            }

            float remaining = ready - now;
            return remaining > 0f ? remaining : 0f;
        }

        public void Begin(string spellId, float now, float cooldown)
        {
            if (string.IsNullOrEmpty(spellId))
            {
                return;
            }

            if (cooldown <= 0f)
            {
                readyAt.Remove(spellId);
                return;
            }

            readyAt[spellId] = now + cooldown;
        }

        public void Clear() => readyAt.Clear();

        public void Clear(string spellId)
        {
            if (!string.IsNullOrEmpty(spellId))
            {
                readyAt.Remove(spellId);
            }
        }
    }
}
