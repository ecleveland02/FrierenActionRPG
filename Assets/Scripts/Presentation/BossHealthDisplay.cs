using Frieren.Characters;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>Compact scene-independent boss bar shown after the dragon engages.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealth))]
    [RequireComponent(typeof(Frieren.Enemies.EnemyPerception))]
    public sealed class BossHealthDisplay : MonoBehaviour
    {
        [SerializeField] private string bossName = "Solar Dragon";
        private CharacterHealth health;
        private Frieren.Enemies.EnemyPerception perception;
        private GUIStyle nameStyle;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            perception = GetComponent<Frieren.Enemies.EnemyPerception>();
        }

        private void OnGUI()
        {
            if (health == null || !health.IsAlive || perception == null || !perception.HasTarget) return;
            float width = Mathf.Min(720f, Screen.width * .62f);
            var area = new Rect((Screen.width - width) * .5f, 34f, width, 46f);
            GUI.Box(area, GUIContent.none);
            nameStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            GUI.Label(new Rect(area.x, area.y, area.width, 22f), bossName, nameStyle);
            float ratio = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
            var back = new Rect(area.x + 12f, area.y + 26f, area.width - 24f, 12f);
            GUI.color = new Color(.12f, .04f, .03f, .95f); GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = new Color(.86f, .22f, .08f, 1f);
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * ratio, back.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
