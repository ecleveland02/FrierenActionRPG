using System;
using Frieren.Core.Debugging;
using Frieren.Core.Magic;
using UnityEngine;

namespace Frieren.World
{
    /// <summary>
    /// Something a Force pulse lifts into the air, holds, and then lets fall.
    /// </summary>
    /// <remarks>
    /// Deliberately not physics driven. A kinematic lift is predictable, the same reason the
    /// character uses a CharacterController rather than a Rigidbody: a block that must become a step
    /// to reach a ledge has to arrive where the designer said, not where the solver put it.
    ///
    /// It reacts to Force, so anything that emits Force moves it - levitation now, a shockwave or a
    /// gust later, with no change here.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LevitatableObject : MonoBehaviour, IMagicReceiver
    {
        [Header("Lift")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Force that must arrive in one pulse to lift this at all.")]
        private float forceThreshold = 5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Metres lifted at the threshold. Stronger pulses lift proportionally further.")]
        private float liftPerPulse = 2.5f;

        [SerializeField]
        [Min(0f)]
        private float maximumLift = 5f;

        [SerializeField]
        [Min(0.01f)]
        private float riseSpeed = 4f;

        [SerializeField]
        [Min(0.01f)]
        private float fallSpeed = 2.5f;

        [Header("Hold")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds held at height before it sinks back. Long enough to be walked on.")]
        private float holdDuration = 4f;

        private Vector3 restingPosition;
        private float targetLift;
        private float holdRemaining;

        public event Action<float> Lifted;

        public event Action Returned;

        /// <summary>Current height above its resting place.</summary>
        public float CurrentLift => transform.position.y - restingPosition.y;

        public bool IsRaised => CurrentLift > 0.01f;

        private void Awake() => restingPosition = transform.position;

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            if (holdRemaining > 0f)
            {
                holdRemaining -= deltaTime;

                if (holdRemaining <= 0f)
                {
                    targetLift = 0f;
                }
            }

            float current = CurrentLift;

            if (Mathf.Approximately(current, targetLift))
            {
                return;
            }

            float speed = targetLift > current ? riseSpeed : fallSpeed;
            float next = Mathf.MoveTowards(current, targetLift, speed * deltaTime);
            transform.position = restingPosition + Vector3.up * next;

            if (next <= 0.001f && targetLift <= 0f && current > 0.001f)
            {
                Returned?.Invoke();
            }
        }

        public bool ReceiveMagic(in MagicPulse pulse)
        {
            if (pulse.Element != MagicElement.Force || pulse.Magnitude < forceThreshold)
            {
                return false;
            }

            float scale = forceThreshold <= 0f ? 1f : pulse.Magnitude / forceThreshold;
            targetLift = Mathf.Min(maximumLift, CurrentLift + liftPerPulse * scale);
            holdRemaining = holdDuration;

            GameLog.Info(LogChannel.Interaction, $"{name} lifted to {targetLift:0.##}m.", this);
            Lifted?.Invoke(targetLift);
            return true;
        }

        /// <summary>Drops it immediately. For resetting a puzzle.</summary>
        public void ReturnToRest()
        {
            targetLift = 0f;
            holdRemaining = 0f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 basePosition = Application.isPlaying ? restingPosition : transform.position;
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            Gizmos.DrawLine(basePosition, basePosition + Vector3.up * maximumLift);
            Gizmos.DrawWireCube(basePosition + Vector3.up * maximumLift, Vector3.one * 0.3f);
        }
#endif
    }
}
