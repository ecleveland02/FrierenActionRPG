using UnityEngine;
using UnityEngine.UI;

namespace Frieren.UI
{
    /// <summary>
    /// Makes the UGUI objects the HUD is assembled from. No layout decisions, just construction.
    /// </summary>
    /// <remarks>
    /// Split out so <see cref="HudRoot"/> reads as a description of the screen rather than three
    /// hundred lines of AddComponent. Every one of these returns the thing it made, so the caller
    /// keeps the reference and nothing has to be found again later.
    ///
    /// Legacy <c>Text</c> rather than TextMeshPro, and deliberately. TMP needs a font asset built in
    /// the editor, and this project has none; a HUD that depends on an asset nobody has created
    /// renders as nothing with no error. <c>LegacyRuntime.ttf</c> ships inside the engine and is
    /// always there. When a real font is chosen, this is the one file that changes.
    /// </remarks>
    public static class HudBuilder
    {
        private static Font builtinFont;

        /// <summary>The engine's own font. Available in a build as well as in the editor.</summary>
        public static Font Font
        {
            get
            {
                if (builtinFont == null)
                {
                    builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return builtinFont;
            }
        }

        /// <summary>
        /// A screen-space canvas that scales with resolution.
        /// </summary>
        /// <remarks>
        /// ScaleWithScreenSize against a 1920x1080 reference, matched half on width and half on
        /// height. Constant pixel size is the default and is wrong for a game: the HUD would be
        /// half the apparent size on a 4K monitor and swamp a small window.
        ///
        /// No GraphicRaycaster. Nothing here is clickable, and a raycaster on a full-screen canvas
        /// silently eats pointer events that the game wants.
        /// </remarks>
        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>A stretchable coloured rectangle. The HUD is mostly these.</summary>
        public static Image CreateImage(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = colour;

            // Nothing in the HUD is interactive, and a graphic that takes raycasts blocks whatever
            // is behind it for free.
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(Transform parent, string name, int size, TextAnchor alignment,
            Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = colour;
            text.raycastTarget = false;

            // Shrink rather than clip. A cut-off number is worse than a small one.
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Anchors a rect to a corner and places it by offset, in reference-resolution pixels.
        /// </summary>
        /// <remarks>
        /// Anchor and pivot are set to the same corner so the offset means the same thing at any
        /// resolution: distance from that corner. Mixing them is the usual reason a HUD element
        /// drifts when the window is resized.
        /// </remarks>
        public static RectTransform Place(Component target, Vector2 corner, Vector2 offset, Vector2 size)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        /// <summary>
        /// A bar: a background, a slow ghost fill, and the real fill on top.
        /// </summary>
        /// <remarks>
        /// Filled image type rather than a scaled rect, so the fill grows from one edge instead of
        /// from its centre and needs no pivot arithmetic to stay put.
        /// </remarks>
        public static void CreateBar(Transform parent, string name, Vector2 corner, Vector2 offset,
            Vector2 size, Color fill, Color ghost, out Image fillImage, out Image ghostImage,
            out Text label)
        {
            Image background = CreateImage(parent, name, new Color(0.05f, 0.06f, 0.09f, 0.78f));
            Place(background, corner, offset, size);

            ghostImage = CreateImage(background.transform, "Ghost", ghost);
            Stretch(ghostImage, 2f);
            ghostImage.type = Image.Type.Filled;
            ghostImage.fillMethod = Image.FillMethod.Horizontal;

            fillImage = CreateImage(background.transform, "Fill", fill);
            Stretch(fillImage, 2f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;

            label = CreateText(background.transform, "Label", 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(label, 0f);
        }

        /// <summary>Fills the parent, inset by a margin on every side.</summary>
        public static RectTransform Stretch(Component target, float inset)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }
    }
}
