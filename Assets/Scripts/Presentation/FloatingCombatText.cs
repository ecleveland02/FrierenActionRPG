using System.Collections.Generic;
using UnityEngine;

namespace Frieren.Presentation
{
    /// <summary>
    /// Draws numbers that rise from a character and fade: damage taken, damage a barrier ate.
    /// </summary>
    /// <remarks>
    /// The point is not decoration - it is that combat tuning is currently guesswork. Eighty enemy
    /// health and forty-five for Zoltraak are numbers nobody has watched land. Seeing them makes
    /// "this fight is too long" into "this fight is four Zoltraaks and I have mana for three".
    ///
    /// IMGUI in a world-projected rect, for the same reason the rest of the debug tooling uses it:
    /// no canvas, no prefab, nothing to conflict with the real HUD when there is one. It is a
    /// measuring instrument with a short life expectancy.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FloatingCombatText : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int MaxEntries = 12;

        [SerializeField]
        [Tooltip("Height above the character's feet that numbers start from.")]
        private float originHeight = 2.1f;

        [SerializeField]
        [Tooltip("Metres a number rises over its life.")]
        private float rise = 0.9f;

        [SerializeField] private float lifetime = 1.1f;

        private readonly List<Entry> entries = new List<Entry>(MaxEntries);
        private GUIStyle style;

        private struct Entry
        {
            public string Text;
            public Color Colour;
            public float BornAt;
            public float Spread;
        }

        public void Show(string text, Color colour)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (entries.Count >= MaxEntries)
            {
                entries.RemoveAt(0);
            }

            entries.Add(new Entry
            {
                Text = text,
                Colour = colour,
                BornAt = Time.time,
                // Numbers arriving together would stack exactly on top of each other and read as one.
                Spread = Random.Range(-28f, 28f),
            });
        }

        private void Update()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (Time.time - entries[i].BornAt >= lifetime)
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private void OnGUI()
        {
            if (entries.Count == 0)
            {
                return;
            }

            UnityEngine.Camera view = UnityEngine.Camera.main;

            if (view == null)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };

            Vector3 origin = transform.position + Vector3.up * originHeight;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                float age = Mathf.Clamp01((Time.time - entry.BornAt) / lifetime);
                Vector3 world = origin + Vector3.up * (rise * age);
                Vector3 screen = view.WorldToScreenPoint(world);

                // Behind the camera projects to a mirrored point in front of it, which would draw
                // numbers for things you cannot see.
                if (screen.z <= 0f)
                {
                    continue;
                }

                Color colour = entry.Colour;
                colour.a = 1f - age * age;

                Color previous = GUI.color;
                GUI.color = colour;
                GUI.Label(new Rect(screen.x - 40f + entry.Spread, Screen.height - screen.y - 10f, 120f, 22f),
                    entry.Text, style);
                GUI.color = previous;
            }
        }
#else
        public void Show(string text, Color colour)
        {
        }
#endif
    }
}
