using Frieren.Characters;
using Frieren.Enemies;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>Scene-owned soundtrack; observes combat without changing gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class EncounterMusic : MonoBehaviour
    {
        [SerializeField] private EnemySpawner[] encounters = new EnemySpawner[0];
        [SerializeField] private AudioSource exploration;
        [SerializeField] private AudioSource combat;
        [SerializeField] private AudioClip[] combatTracks = new AudioClip[0];
        [SerializeField] private AudioSource forest;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.38f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 2f;
        [SerializeField, Min(0f)] private float calmDelay = 4f;
        private readonly CombatMusicState mood = new CombatMusicState();
        private float blend;
        private int lastCombatTrack = -1;
        private bool wasInCombat;
        private AudioClip bossTrack;
        public bool InCombat => mood.InCombat;
        public float CombatBlend => blend;

        private void OnEnable()
        {
            if (encounters == null || encounters.Length == 0)
                encounters = FindObjectsByType<EnemySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            mood.Clear();
            blend = 0f;
            wasInCombat = false;
            StartLoop(exploration, musicVolume);
            StartLoop(combat, 0f);
            StartLoop(forest, 0.2f);
        }

        private static void StartLoop(AudioSource source, float volume)
        {
            if (source == null || source.clip == null) return;
            source.loop = true;
            source.volume = volume;
            source.Play();
        }

        public bool HasActiveThreat()
        {
            for (int i = 0; i < encounters.Length; i++)
            {
                if (encounters[i] == null) continue;
                var enemies = encounters[i].Spawned;
                for (int j = 0; j < enemies.Count; j++)
                {
                    GameObject enemy = enemies[j];
                    if (enemy == null || !enemy.activeInHierarchy) continue;
                    var perception = enemy.GetComponent<EnemyPerception>();
                    if (perception == null || !perception.HasTarget) continue;
                    var targetHealth = perception.Target.GetComponentInParent<CharacterHealth>();
                    if (targetHealth != null && !targetHealth.IsAlive) continue;
                    if (enemy.TryGetComponent(out EnemyBrain brain) && brain.isActiveAndEnabled &&
                        (brain.State == EnemyState.Chase || brain.State == EnemyState.Attack ||
                         brain.State == EnemyState.Stagger)) return true;
                }
            }
            return false;
        }

        private void Update()
        {
            // Game time freezes the hold during pause; unscaled time keeps the fade smooth in slow motion.
            mood.Tick(HasActiveThreat(), Time.deltaTime, calmDelay);
            if (mood.InCombat && !wasInCombat) SelectCombatTrack();
            wasInCombat = mood.InCombat;
            blend = Mathf.MoveTowards(blend, mood.InCombat ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.1f, fadeSeconds));
            if (exploration != null) exploration.volume = musicVolume * Mathf.Cos(blend * Mathf.PI * 0.5f);
            if (combat != null) combat.volume = musicVolume * Mathf.Sin(blend * Mathf.PI * 0.5f);
            if (forest != null) forest.volume = Mathf.Lerp(0.20f, 0.09f, blend);
        }

        private void SelectCombatTrack()
        {
            if (bossTrack != null) return;
            if (combat == null || combatTracks == null || combatTracks.Length == 0) return;
            int index = CombatTrackSelection.Choose(
                combatTracks.Length, lastCombatTrack, Random.Range(0, combatTracks.Length));
            AudioClip selected = combatTracks[index];
            if (selected == null) return;
            lastCombatTrack = index;
            if (combat.clip == selected && combat.isPlaying) return;
            combat.clip = selected;
            combat.Play();
        }

        public void BeginBossMusic(AudioClip track)
        {
            if (track == null || combat == null) return;
            bossTrack = track;
            if (combat.clip == track && combat.isPlaying) return;
            combat.clip = track;
            combat.loop = true;
            combat.Play();
        }

        public void EndBossMusic(AudioClip track)
        {
            if (bossTrack != track) return;
            bossTrack = null;
            if (mood.InCombat) SelectCombatTrack();
        }

        private void OnDisable()
        {
            if (exploration != null) exploration.Stop();
            if (combat != null) combat.Stop();
            if (forest != null) forest.Stop();
            mood.Clear();
            wasInCombat = false;
            bossTrack = null;
        }
    }
}
