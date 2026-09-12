using System.Collections.Generic;
using Frieren.Characters;
using Frieren.Characters.Animation;
using Frieren.Core;
using Frieren.Enemies;
using Frieren.Magic;
using UnityEngine;

namespace Frieren.Tests.PlayMode
{
    /// <summary>
    /// Builds throwaway characters, spells and ground for a play-mode test, and tears them down.
    /// </summary>
    /// <remarks>
    /// Deliberately builds from code rather than instantiating the project's prefabs. A test that
    /// loads <c>Player.prefab</c> would fail whenever the prefab changed, and would be testing the
    /// YAML rather than the behaviour; the YAML has its own checker
    /// (<c>Tools/Validation/check_unity_yaml.py</c>) and its own smoke test that loads the real
    /// scene. Everything here is about what the components do when wired correctly.
    ///
    /// Objects are tracked so <see cref="Dispose"/> can remove them. Play-mode tests share one
    /// scene, so anything left behind is a collider the next test will trip over.
    /// </remarks>
    public sealed class TestWorld
    {
        private readonly List<Object> spawned = new List<Object>();

        public GameObject Ground { get; private set; }

        /// <summary>A wide, solid floor on the Ground layer, so motors have something to stand on.</summary>
        public GameObject CreateGround(float size = 60f)
        {
            if (Ground != null)
            {
                return Ground;
            }

            Ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Ground.name = "TestGround";
            Ground.layer = GameLayers.Ground;
            Ground.transform.position = new Vector3(0f, -0.5f, 0f);
            Ground.transform.localScale = new Vector3(size, 1f, size);
            Track(Ground);
            return Ground;
        }

        public CharacterStatsDefinition CreateStats(float maxHealth = 100f, float maxMana = 100f,
            float healthRegenPerSecond = 0f, float healthRegenDelay = 5f,
            float manaRegenPerSecond = 0f, float manaRegenDelay = 1.5f)
        {
            var stats = ScriptableObject.CreateInstance<CharacterStatsDefinition>();
            stats.name = "Stats_Test";
            TestFields.Set(stats, "maxHealth", maxHealth);
            TestFields.Set(stats, "maxMana", maxMana);
            TestFields.Set(stats, "healthRegenPerSecond", healthRegenPerSecond);
            TestFields.Set(stats, "healthRegenDelay", healthRegenDelay);
            TestFields.Set(stats, "manaRegenPerSecond", manaRegenPerSecond);
            TestFields.Set(stats, "manaRegenDelay", manaRegenDelay);
            Track(stats);
            return stats;
        }

        /// <summary>
        /// A character with everything <c>Frieren.Characters</c> provides and nothing that drives it.
        /// </summary>
        /// <remarks>
        /// Assembled while the GameObject is inactive and activated at the end, so every component's
        /// <c>Awake</c> runs once against a finished character. Adding components to a live object
        /// instead means <c>CharacterStats.Awake</c> fires before its definition is assigned and logs
        /// an error - and a logged error fails a Unity test, so the whole fixture would collapse for
        /// a reason that has nothing to do with what is being tested.
        /// </remarks>
        public GameObject CreateCharacter(string name, Vector3 position, int layer,
            CharacterStatsDefinition stats = null, bool activate = true)
        {
            var host = new GameObject(name) { layer = layer };
            host.SetActive(false);
            host.transform.position = position;
            Track(host);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.layer = layer;
            visual.transform.SetParent(host.transform, false);
            visual.transform.localPosition = Vector3.up;
            Object.Destroy(visual.GetComponent<Collider>());

            CharacterController controller = host.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.up;

            host.AddComponent<CharacterMotor>();
            host.AddComponent<CharacterActionLock>();
            host.AddComponent<PlaceholderCharacterAnimation>();

            CharacterStats statsComponent = host.AddComponent<CharacterStats>();
            statsComponent.SetDefinition(stats != null ? stats : CreateStats());

            host.AddComponent<CharacterHealth>();
            host.AddComponent<CharacterMana>();

            if (activate)
            {
                host.SetActive(true);
            }

            return host;
        }

        /// <summary>A character that can decide things: perception, melee and a brain.</summary>
        public GameObject CreateEnemy(string name, Vector3 position, CharacterStatsDefinition stats = null)
        {
            GameObject host = CreateCharacter(name, position, GameLayers.Enemy, stats, activate: false);

            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(host.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            EnemyPerception perception = host.AddComponent<EnemyPerception>();
            TestFields.Set(perception, "eyes", eyes.transform);

            host.AddComponent<EnemyMelee>();
            host.AddComponent<EnemyBrain>();

            host.SetActive(true);
            return host;
        }

        /// <summary>A pulse effect that emits one element, for spells that act on the world or on wards.</summary>
        public MagicPulseEffect CreatePulseEffect(Core.Magic.MagicElement element, float magnitude,
            float radius = 0f)
        {
            var effect = ScriptableObject.CreateInstance<MagicPulseEffect>();
            effect.name = $"Effect_Test_{element}";
            TestFields.Set(effect, "element", element);
            TestFields.Set(effect, "magnitude", magnitude);
            TestFields.Set(effect, "radius", radius);
            Track(effect);
            return effect;
        }

        public DealDamageEffect CreateDamageEffect(float amount, DamageType type = DamageType.Arcane,
            float radius = 0f)
        {
            var effect = ScriptableObject.CreateInstance<DealDamageEffect>();
            effect.name = "Effect_Test_Damage";
            TestFields.Set(effect, "amount", amount);
            TestFields.Set(effect, "damageType", type);
            TestFields.Set(effect, "radius", radius);
            Track(effect);
            return effect;
        }

        public SpellDefinition CreateSpell(string id, SpellTargeting targeting, params SpellEffect[] effects)
        {
            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.name = $"Spell_Test_{id}";
            TestFields.Set(spell, "id", id);
            TestFields.Set(spell, "displayName", id);
            TestFields.Set(spell, "targeting", targeting);
            TestFields.Set(spell, "effects", new List<SpellEffect>(effects));
            TestFields.Set(spell, "castTime", 0f);
            TestFields.Set(spell, "cooldown", 0f);
            TestFields.Set(spell, "manaCost", 0f);
            TestFields.Set(spell, "range", 30f);
            Track(spell);
            return spell;
        }

        public T Track<T>(T target) where T : Object
        {
            spawned.Add(target);
            return target;
        }

        public void Dispose()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    Object.Destroy(spawned[i]);
                }
            }

            spawned.Clear();
            Ground = null;
        }
    }
}
