using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuestLAN.NetworkSandbox.Editor
{
    /// <summary>Explicit local request bridge for an already-open Editor; no action without a request.</summary>
    [InitializeOnLoad]
    public static class NetworkSandboxValidation
    {
        private const string RequestPath = "Logs/QuestLAN-M2-Validation.request";
        private const string StatusPath = "Logs/QuestLAN-M2-Validation.txt";
        private const string ResultPath = "Logs/QuestLAN-M2-PlayMode.xml";
        private static readonly Callbacks callbacks = new Callbacks();
        private static TestRunnerApi api;

        static NetworkSandboxValidation()
        {
            TestRunnerApi.RegisterTestCallback(callbacks);
            EditorApplication.update += CheckRequest;
        }

        private static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            if (SessionState.GetBool("QuestLAN.M2.TestsRunning", false)) return;
            // Only the literal supported command is accepted, never code or arbitrary paths.
            string command;
            try { command = File.ReadAllText(RequestPath).Trim(); }
            catch (IOException) { return; } // Writer may still be publishing a request.
            if (command != "validate-mock" && command != "preview-mock" && command != "setup-lan" && command != "build-lan" && command != "preview-lan" && command != "validate-lan" && command != "recompile-lan") return;
            File.Move(RequestPath, RequestPath + ".accepted." + DateTime.UtcNow.Ticks);
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save your unsaved scenes before running PlayMode validation.");
                if (command == "recompile-lan") { CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache); return; }
                if (command == "setup-lan") { LanSandboxBuilder.Create(); return; }
                if (command == "build-lan") { LanSandboxBuilder.BuildWindows(); return; }
                if (command == "preview-lan")
                {
                    EditorSceneManager.OpenScene(LanSandboxBuilder.ScenePath);
                    EditorApplication.isPlaying = true;
                    return;
                }
                if (command == "preview-mock")
                {
                    EditorSceneManager.OpenScene("Assets/NetworkingSandbox/Scenes/NetworkSandbox.unity");
                    EditorApplication.isPlaying = true;
                    return;
                }
                File.WriteAllText(StatusPath, "Unity Editor compiled validation tool. Starting sandbox setup.\n");
                if (!File.Exists("Assets/NetworkingSandbox/Scenes/NetworkSandbox.unity"))
                    NetworkSandboxBuilder.Create();
                File.AppendAllText(StatusPath, "Sandbox assets generated. Starting PlayMode tests.\n");
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                SessionState.SetBool("QuestLAN.M2.TestsRunning", true);
                api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.PlayMode,
                    assemblyNames = command == "validate-lan" ? new[] { "QuestLAN.Simulation.Tests", "QuestLAN.Networking.Tests" } : new[] { "QuestLAN.Simulation.Tests" }
                }));
            }
            catch (Exception exception)
            {
                SessionState.SetBool("QuestLAN.M2.TestsRunning", false);
                File.AppendAllText(StatusPath, exception + "\n");
                Debug.LogException(exception);
            }
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool("QuestLAN.M2.TestsRunning", false)) return;
                SessionState.SetBool("QuestLAN.M2.TestsRunning", false);
                if (!File.Exists(StatusPath)) return;
                TestRunnerApi.SaveResultToFile(result, ResultPath);
                File.AppendAllText(StatusPath, $"PlayMode {result.TestStatus}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}\n");
            }
        }
    }
}
