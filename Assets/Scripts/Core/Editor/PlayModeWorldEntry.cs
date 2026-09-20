using UnityEditor;
using UnityEditor.SceneManagement;

namespace Frieren.Core.EditorTools
{
    [InitializeOnLoad]
    public static class PlayModeWorldEntry
    {
        private const string WorldScene = "Assets/Scenes/FrierenOpenWorld.unity";
        static PlayModeWorldEntry() => EditorApplication.delayCall += Configure;
        private static void Configure()
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldScene);
            if (scene != null && EditorSceneManager.playModeStartScene != scene)
                EditorSceneManager.playModeStartScene = scene;
        }
    }
}
