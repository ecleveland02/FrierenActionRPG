using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Characters
{
    /// <summary>
    /// Draws a character's health and mana on screen, and provides keys to change them.
    /// </summary>
    /// <remarks>
    /// There is no HUD yet and nothing in the world that can hurt you, so without this the vitals
    /// are invisible and untestable in play. IMGUI for the same reason the core debug overlay uses
    /// it: no canvas, no prefab, no setup, and nothing to collide with the real UI later.
    ///
    /// This is the only reason <c>Frieren.Characters</c> references the Input System package. That
    /// is a package reference rather than a project one, so it does not affect the assembly
    /// dependency direction, but it should go when this component does - once a real HUD exists and
    /// enemies can deal damage.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterVitalsReadout : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float TestAmount = 15f;

        [SerializeField] private bool showKeys = true;

        private CharacterHealth health;
        private CharacterMana mana;
        private CharacterLevitation levitation;
        private CharacterMotor motor;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            mana = GetComponent<CharacterMana>();
            levitation = GetComponent<CharacterLevitation>();
            motor = GetComponent<CharacterMotor>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f2Key.wasPressedThisFrame && health != null)
            {
                health.TakeDamage(new DamageInfo(TestAmount, DamageType.Physical, gameObject));
            }

            if (keyboard.f3Key.wasPressedThisFrame && health != null)
            {
                if (health.IsAlive)
                {
                    health.Heal(TestAmount);
                }
                else
                {
                    health.Revive();
                }
            }

            if (keyboard.f4Key.wasPressedThisFrame && mana != null)
            {
                mana.TrySpend(TestAmount);
            }
        }

        private void OnGUI()
        {
            float height = (showKeys ? 76f : 56f) + (levitation != null ? 18f : 0f);
            var area = new Rect(10f, 275f, 420f, height);
            GUILayout.BeginArea(area, GUI.skin.box);

            if (health != null)
            {
                GUILayout.Label($"Health {health.Current:0}/{health.Max:0}" +
                                $"   ({health.Normalized:P0})   {(health.IsAlive ? "alive" : "DEAD")}");
            }

            if (mana != null)
            {
                GUILayout.Label($"Mana   {mana.Current:0}/{mana.Max:0}   ({mana.Normalized:P0})");
            }

            if (levitation != null)
            {
                GUILayout.Label($"Levitating {(levitation.IsLevitating ? "YES" : "no")}" +
                                $"   vertical {(motor != null ? motor.VerticalVelocity : 0f):0.0} m/s" +
                                $"   gravity {(motor != null && motor.GravityEnabled ? "on" : "OFF")}");
            }

            if (showKeys)
            {
                GUILayout.Label($"F2 damage {TestAmount}   F3 heal / revive   F4 spend {TestAmount} mana");
            }

            GUILayout.EndArea();
        }
#endif
    }
}
