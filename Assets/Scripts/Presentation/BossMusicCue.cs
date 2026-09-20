using Frieren.Characters;
using Frieren.Enemies;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>Claims the shared combat channel while this boss is engaged.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealth))]
    [RequireComponent(typeof(EnemyPerception))]
    public sealed class BossMusicCue : MonoBehaviour
    {
        [SerializeField] private AudioClip music;
        private CharacterHealth health;
        private EnemyPerception perception;
        private EncounterMusic soundtrack;
        private bool playing;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            perception = GetComponent<EnemyPerception>();
        }

        private void OnEnable() => health.Died += OnDied;

        private void OnDisable()
        {
            health.Died -= OnDied;
            StopMusic();
        }

        private void Update()
        {
            if (soundtrack == null) soundtrack = FindFirstObjectByType<EncounterMusic>();
            bool engaged = health.IsAlive && perception.HasTarget;
            if (engaged && !playing && soundtrack != null)
            {
                soundtrack.BeginBossMusic(music);
                playing = true;
            }
            else if (!engaged && playing) StopMusic();
        }

        private void OnDied(DamageInfo damage) => StopMusic();

        private void StopMusic()
        {
            if (soundtrack != null) soundtrack.EndBossMusic(music);
            playing = false;
        }
    }
}
