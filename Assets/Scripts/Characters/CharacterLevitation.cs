using System;
using Frieren.Core.Debugging;
using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.Characters
{
    /// <summary>
    /// Lets a character be lifted by Force magic, including its own.
    /// </summary>
    /// <remarks>
    /// The character's answer to the same contract a crate uses. Levitation does not need to know
    /// whether it is pointed at a block or at the caster; both are receivers, and both respond to
    /// Force. That is what makes "lift the block onto the ledge" and "lift yourself onto the ledge"
    /// two solutions to one problem rather than two features.
    ///
    /// Gravity is suspended rather than countered. Writing an opposing velocity each frame still
    /// loses one frame of acceleration per frame, which reads as an unexplained slow sink.
    ///
    /// The hover lapses shortly after the last pulse, so a channelled spell holds it up and
    /// releasing lets it down.
    /// </remarks>
    [RequireComponent(typeof(CharacterMotor))]
    [DisallowMultipleComponent]
    public sealed class CharacterLevitation : MonoBehaviour, IMagicReceiver
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("Force that must arrive in one pulse to lift this character at all.")]
        private float forceThreshold = 5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Metres above the height where the hover began that the character can rise to.")]
        private float maximumRise = 4f;

        [SerializeField]
        [Min(0.01f)]
        private float riseSpeed = 3.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How long a single pulse keeps the character aloft. A channel refreshes this every tick.")]
        private float hoverDuration = 0.35f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Downward speed when the hover lapses. Gentler than a fall, so landing is not a drop.")]
        private float settleSpeed = 2.5f;

        private CharacterMotor motor;
        private float hoverRemaining;
        private float ceilingY;
        private bool settling;

        public event Action Began;

        public event Action Ended;

        public bool IsLevitating { get; private set; }

        private void Awake() => motor = GetComponent<CharacterMotor>();

        private void OnDisable() => Stop();

        public bool ReceiveMagic(in MagicPulse pulse)
        {
            if (pulse.Element != MagicElement.Force || pulse.Magnitude < forceThreshold)
            {
                return false;
            }

            if (!IsLevitating)
            {
                Begin();
            }

            hoverRemaining = hoverDuration;
            return true;
        }

        private void Begin()
        {
            IsLevitating = true;
            settling = false;
            ceilingY = transform.position.y + maximumRise;
            motor.GravityEnabled = false;
            GameLog.Info(LogChannel.Magic, $"{name} began levitating.", this);
            Began?.Invoke();
        }

        private void Update()
        {
            if (!IsLevitating)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            hoverRemaining -= deltaTime;

            if (hoverRemaining <= 0f && !settling)
            {
                // Settle rather than drop: gravity resuming from a standstill four metres up reads
                // as the spell having failed rather than having ended.
                settling = true;
            }

            if (settling)
            {
                motor.SetVerticalVelocity(-settleSpeed);

                if (motor.IsGrounded)
                {
                    Stop();
                }

                return;
            }

            float remaining = ceilingY - transform.position.y;
            motor.SetVerticalVelocity(Mathf.Clamp(remaining, -1f, 1f) * riseSpeed);
        }

        /// <summary>Ends the hover immediately and hands the vertical axis back to gravity.</summary>
        public void Stop()
        {
            if (!IsLevitating)
            {
                return;
            }

            IsLevitating = false;
            settling = false;
            hoverRemaining = 0f;

            if (motor != null)
            {
                motor.GravityEnabled = true;
                motor.SetVerticalVelocity(0f);
            }

            GameLog.Info(LogChannel.Magic, $"{name} stopped levitating.", this);
            Ended?.Invoke();
        }
    }
}
