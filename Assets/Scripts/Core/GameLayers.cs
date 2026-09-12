namespace Frieren.Core
{
    /// <summary>
    /// The project's physics layers, by index and as masks.
    /// </summary>
    /// <remarks>
    /// Layer numbers are decided in ProjectSettings and then referenced from code, prefabs and
    /// scenes. Writing them as literals spreads a settings file's contents across the codebase, and
    /// the day a layer is inserted every one of those literals is silently wrong. Naming them here
    /// makes that a one-file change.
    ///
    /// Masks are here so a serialized <c>LayerMask</c> field can take a sensible default from its
    /// C# initializer instead of being hand-written into a YAML asset, where a mistake reads as a
    /// mask of zero and simply stops working with no error.
    ///
    /// These must stay in step with <c>ProjectSettings/TagManager.asset</c>. There is no way to
    /// assert that at compile time; the editor validator checks it at import instead.
    /// </remarks>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int Player = 8;
        public const int Enemy = 9;
        public const int Npc = 10;
        public const int Ground = 11;
        public const int Interactable = 12;
        public const int MagicTarget = 13;
        public const int Projectile = 14;
        public const int Trigger = 15;

        public const int DefaultMask = 1 << Default;
        public const int PlayerMask = 1 << Player;
        public const int EnemyMask = 1 << Enemy;
        public const int NpcMask = 1 << Npc;
        public const int GroundMask = 1 << Ground;
        public const int InteractableMask = 1 << Interactable;
        public const int MagicTargetMask = 1 << MagicTarget;

        /// <summary>Everything a spell or an attack should be able to hit or stop against.</summary>
        public const int CombatantsMask = PlayerMask | EnemyMask | NpcMask | MagicTargetMask;

        /// <summary>Solid world geometry. What blocks sight, aim and movement.</summary>
        public const int SolidMask = DefaultMask | GroundMask;
    }
}
