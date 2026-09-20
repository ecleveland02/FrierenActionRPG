using Frieren.Characters.Animation;
using Frieren.Enemies;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>Maps the shared enemy state machine onto the dragon's bespoke clips.</summary>
    [DisallowMultipleComponent]
    public sealed class SolarDragonBossAnimation : MonoBehaviour, ICharacterAnimation
    {
        [SerializeField] private Animator animator;
        private EnemyBrain brain;
        private EnemyMelee melee;
        private float actionUntil;
        private int attackIndex;
        private string state;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            melee = GetComponent<EnemyMelee>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable() { if (melee != null) melee.SwingStarted += OnAttack; }
        private void OnDisable() { if (melee != null) melee.SwingStarted -= OnAttack; }

        private void OnAttack()
        {
            string[] attacks = { "Attack_Bite", "Attack_Wing", "Attack_Tail" };
            Play(attacks[attackIndex++ % attacks.Length], .1f);
            actionUntil = Time.time + 1.25f;
        }

        private void LateUpdate()
        {
            if (brain == null || animator == null) return;
            if (brain.State == EnemyState.Dead) { Play("Death", .2f); return; }
            if (Time.time < actionUntil) return;
            Play(brain.State == EnemyState.Chase ? "Walk" : "Idle", .2f);
        }

        private void Play(string next, float fade)
        {
            if (state == next || animator == null) return;
            state = next;
            animator.CrossFadeInFixedTime(next, fade);
        }

        public void SetLocomotion(float planarSpeed, float normalizedSpeed, bool isGrounded, float verticalVelocity) { }

        public void PlayAction(CharacterAction action)
        {
            if (action == CharacterAction.Death) Play("Death", .15f);
            else if (action == CharacterAction.Hit)
            {
                Play("Hit", .08f);
                actionUntil = Time.time + .35f;
            }
        }
    }
}
