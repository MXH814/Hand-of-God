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
        private const string ScenePath02 = "Assets/Scenes/Level02.unity";
        private const string ScenePath03 = "Assets/Scenes/Level03.unity";
        private const string ScenePath04 = "Assets/Scenes/Level04.unity";

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
                new EditorBuildSettingsScene(ScenePath02, true),
                new EditorBuildSettingsScene(ScenePath03, true),
                new EditorBuildSettingsScene(ScenePath04, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Hand of God Level01 scene rebuilt.");
        }

        [MenuItem("Hand of God/Rebuild Level 02 Scene")]
        public static void RebuildLevel02()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Level02";

            var bootstrap = new GameObject("Game Bootstrap");
            bootstrap.AddComponent<GameBootstrap>().BuildGameWorld();

            EditorSceneManager.SaveScene(scene, ScenePath02);
            // Keep build settings consistent with Level01 style: include both scenes
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ScenePath02, true),
                new EditorBuildSettingsScene(ScenePath03, true),
                new EditorBuildSettingsScene(ScenePath04, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Hand of God Level02 scene rebuilt.");
        }

        [MenuItem("Hand of God/Rebuild Level 03 Scene")]
        public static void RebuildLevel03()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Level03";

            var bootstrap = new GameObject("Game Bootstrap");
            bootstrap.AddComponent<GameBootstrap>().BuildGameWorld();

            EditorSceneManager.SaveScene(scene, ScenePath03);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ScenePath02, true),
                new EditorBuildSettingsScene(ScenePath03, true),
                new EditorBuildSettingsScene(ScenePath04, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Hand of God Level03 scene rebuilt.");
        }

        [MenuItem("Hand of God/Rebuild Level 04 Scene")]
        public static void RebuildLevel04()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Level04";

            var bootstrap = new GameObject("Game Bootstrap");
            bootstrap.AddComponent<GameBootstrap>().BuildGameWorld();

            EditorSceneManager.SaveScene(scene, ScenePath04);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ScenePath02, true),
                new EditorBuildSettingsScene(ScenePath03, true),
                new EditorBuildSettingsScene(ScenePath04, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Hand of God Level04 scene rebuilt.");
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

        [MenuItem("Hand of God/Capture Level 02 Preview")]
        public static void CaptureLevel02Preview()
        {
            RebuildLevel02();

            var controller = Object.FindAnyObjectByType<GestureGameController>();
            if (controller != null)
            {
                controller.EditorPreviewLevel2Airflow();
            }

            var camera = Camera.main;
            if (camera == null)
            {
                throw new System.InvalidOperationException("Level02 has no Main Camera.");
            }

            const int width = 1600;
            const int height = 1000;
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Preview"));
            var outputPath = Path.Combine(outputDirectory, "Level02Preview.png");
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
            Debug.Log($"Level02 preview captured: {outputPath}");
        }

        [MenuItem("Hand of God/Capture Level 04 Preview")]
        public static void CaptureLevel04Preview()
        {
            RebuildLevel04();

            var controller = Object.FindAnyObjectByType<GestureGameController>();
            if (controller != null)
            {
                controller.EditorStartLevel(7);
            }

            var camera = Camera.main;
            if (camera == null)
            {
                throw new System.InvalidOperationException("Level04 has no Main Camera.");
            }

            const int width = 1600;
            const int height = 1000;
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Preview"));
            var outputPath = Path.Combine(outputDirectory, "Level04Preview.png");
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
            Debug.Log($"Level04 preview captured: {outputPath}");
        }

        [InitializeOnLoadMethod]
        private static void EnsureLevelSceneExists()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                RebuildLevel01();
            }
            if (!System.IO.File.Exists(ScenePath02))
            {
                RebuildLevel02();
            }
            if (!System.IO.File.Exists(ScenePath03))
            {
                RebuildLevel03();
            }
            if (!System.IO.File.Exists(ScenePath04))
            {
                RebuildLevel04();
            }
        }
    }
}
