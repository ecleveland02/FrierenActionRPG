using UnityEngine;

namespace Frieren.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterStamina : CharacterResource
    {
        public bool TrySpend(float amount) => amount >= 0f && TryDrainExactly(amount);
        protected override float MaxFromStats => Stats != null ? Stats.MaxStamina : 0f;
        protected override float RegenPerSecond => Stats != null ? Stats.StaminaRegenPerSecond : 0f;
        protected override float RegenDelaySeconds => Stats != null ? Stats.StaminaRegenDelay : 0f;
        public bool SpendSprint(float amount)
        {
            if (amount <= 0f) return true;
            bool affordable = Current > amount;
            Drain(amount);
            return affordable;
        }
    }
}
