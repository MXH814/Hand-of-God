using HandOfGod.Gameplay;
using System.IO;
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
            bootstrap.AddComponent<GameBootstrap>().BuildGameWorld();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Hand of God Level01 scene rebuilt.");
        }

        [MenuItem("Hand of God/Capture Level 01 Preview")]
        public static void CaptureLevel01Preview()
        {
            RebuildLevel01();

            var camera = Camera.main;
            if (camera == null)
            {
                throw new System.InvalidOperationException("Level01 has no Main Camera.");
            }

            const int width = 1600;
            const int height = 1000;
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Preview"));
            var outputPath = Path.Combine(outputDirectory, "Level01Preview.png");
            Directory.CreateDirectory(outputDirectory);

            var renderTexture = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(texture);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
            Debug.Log($"Level01 preview captured: {outputPath}");
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
