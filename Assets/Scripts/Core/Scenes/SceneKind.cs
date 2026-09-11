namespace Frieren.Core.Scenes
{
    public enum SceneKind
    {
        /// <summary>The persistent scene holding bootstrap and long-lived systems.</summary>
        Boot = 0,

        /// <summary>Front end / menus.</summary>
        Menu = 1,

        /// <summary>A playable world scene: town, forest, ruins, dungeon.</summary>
        Gameplay = 2,

        /// <summary>Loaded alongside a gameplay scene, e.g. HUD or a sub-area.</summary>
        Additive = 3
    }
}
