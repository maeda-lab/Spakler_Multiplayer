using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;

namespace QuestLAN.Editor
{
    public static class QuestLANIntegrationBuild
    {
        public static void BuildWindowsBatch()
        {
            Directory.CreateDirectory("Build/M3");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainScene.unity", "Assets/NetworkingSandbox/Scenes/LanSandbox.unity" },
                locationPathName = "Build/M3/QuestLANPrototype.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Logs/M3-WindowsBuild.txt", report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("M3 Windows build failed.");
            Debug.Log("M3 Windows build succeeded.");
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            bool startup = settings.InitManagerOnStart;
            try
            {
                // Separate process-test builds avoid creating a graphics XR session in -nographics.
                // This in-memory value is serialized into these builds only, never saved to the project.
                settings.InitManagerOnStart = false;
                BuildProcessTest("Build/M3Headless/QuestLANPrototype.exe", "Assets/Scenes/MainScene.unity", "Logs/M3-HeadlessBuild.txt");
                BuildProcessTest("Build/M3Sandbox/QuestLANSandbox.exe", "Assets/NetworkingSandbox/Scenes/LanSandbox.unity", "Logs/M3-SandboxBuild.txt");
            }
            finally { settings.InitManagerOnStart = startup; }
        }

        private static void BuildProcessTest(string output, string scene, string log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes = new[] { scene }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            File.WriteAllText(log, report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Process-test build failed: " + output);
        }
    }
}
