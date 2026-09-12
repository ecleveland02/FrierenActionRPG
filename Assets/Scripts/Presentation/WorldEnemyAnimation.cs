using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Enemies;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>Imported monster clips follow gameplay; root motion never drives combat.</summary>
    public sealed class WorldEnemyAnimation : MonoBehaviour, ICharacterAnimation
    {
        [SerializeField] private Animator animator;
        private EnemyBrain brain;
        private EnemyMelee melee;
        private float actionUntil;
        private string state;
        private void Awake() { brain = GetComponent<EnemyBrain>(); melee = GetComponent<EnemyMelee>(); }
        private void OnEnable() { if (melee != null) melee.SwingStarted += Swing; }
        private void OnDisable() { if (melee != null) melee.SwingStarted -= Swing; }
        private void Swing() { Play("Attack"); actionUntil = Time.time + 0.9f; }
        private void LateUpdate()
        {
            if (brain == null) return;
            if (brain.State == EnemyState.Dead) { Play("Death"); return; }
            if (Time.time < actionUntil) return;
            Play(brain.State == EnemyState.Chase ? "Run" : "Idle");
        }
        private void Play(string next)
        {
            if (animator == null || state == next) return;
            state = next;
            animator.CrossFadeInFixedTime(next, 0.12f);
        }
        public void SetLocomotion(float planarSpeed, float normalizedSpeed, bool isGrounded, float verticalVelocity) { }
        public void PlayAction(CharacterAction action)
        {
            if (action == CharacterAction.Death) Play("Death");
            else if (action == CharacterAction.Hit) { Play("Hit"); actionUntil = Time.time + 0.4f; }
        }
    }
}
