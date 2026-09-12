namespace Frieren.Core.EditorTools
{
    /// <summary>
    /// Canonical asset paths the editor tooling relies on. Keeping them in one place means a move
    /// breaks a single file rather than three.
    /// </summary>
    internal static class ProjectPaths
    {
        public const string ScenesFolder = "Assets/Scenes";
        public const string BootScene = ScenesFolder + "/Boot.unity";
        public const string TestScene = ScenesFolder + "/TestScene.unity";

        public const string SceneDefinitionsFolder = "Assets/ScriptableObjects/Scenes";
        public const string BootSceneDefinition = SceneDefinitionsFolder + "/Scene_Boot.asset";
        public const string TestSceneDefinition = SceneDefinitionsFolder + "/Scene_TestScene.asset";
        public const string SceneCatalog = SceneDefinitionsFolder + "/SceneCatalog.asset";

        public const string PlayerPrefab = "Assets/Prefabs/Characters/Player.prefab";

        public const string PlayerStats = "Assets/ScriptableObjects/Characters/Stats_Player.asset";

        public const string LogSettings = "Assets/ScriptableObjects/Debug/LogSettings.asset";
        public const string InputReader = "Assets/ScriptableObjects/Input/InputReader.asset";
        public const string InputActions = "Assets/Settings/Input/FrierenControls.inputactions";
    }
}
