using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HandOfGod.EditorTools
{
    public static class GameBuildRunner
    {
        private const string ScenePath = "Assets/Scenes/Level01.unity";
        private const string ScenePath02 = "Assets/Scenes/Level02.unity";
        private const string ScenePath03 = "Assets/Scenes/Level03.unity";
        private const string BuildPath = "Builds/Windows/HandOfGod.exe";

        [MenuItem("Hand of God/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            LevelSceneGenerator.RebuildLevel01();
            LevelSceneGenerator.RebuildLevel02();
            LevelSceneGenerator.RebuildLevel03();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath, ScenePath02, ScenePath03 },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException($"Windows build failed: {report.summary.result}");
            }

            Debug.Log($"Hand of God Windows build created: {Path.GetFullPath(BuildPath)}");
        }
    }
}
