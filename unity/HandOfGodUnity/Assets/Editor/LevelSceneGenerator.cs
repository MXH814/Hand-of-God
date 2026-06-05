using HandOfGod.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandOfGod.EditorTools
{
    public static class LevelSceneGenerator
    {
        private const string ScenePath = "Assets/Scenes/Level01.unity";

        [MenuItem("Hand of God/Rebuild Level 01 Scene")]
        public static void RebuildLevel01()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Level01";

            var bootstrap = new GameObject("Game Bootstrap");
            bootstrap.AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Hand of God Level01 scene rebuilt.");
        }

        [InitializeOnLoadMethod]
        private static void EnsureLevelSceneExists()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                RebuildLevel01();
            }
        }
    }
}
