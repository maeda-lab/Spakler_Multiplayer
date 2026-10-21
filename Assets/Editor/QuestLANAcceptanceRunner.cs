using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.XR.Management;
using UnityEngine;

namespace QuestLAN.Editor
{
    /// <summary>Runs deterministic tests without starting hardware, then restores XR startup.</summary>
    [InitializeOnLoad]
    public static class QuestLANAcceptanceRunner
    {
        private const string Active = "QuestLAN.Acceptance.Active";
        private const string OriginalStartup = "QuestLAN.Acceptance.OriginalStartup";
        private const string BatchAll = "QuestLAN.Acceptance.BatchAll";
        private const string BatchExit = "QuestLAN.Acceptance.BatchExit";
        private const string Request = "Logs/QuestLAN-Acceptance-Request.txt";
        private static double nextPoll;
        private static TestRunnerApi api;

        static QuestLANAcceptanceRunner()
        {
            TestRunnerApi.RegisterTestCallback(new Callbacks());
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (SessionState.GetBool(BatchExit, false))
            {
                // RunFinished fires before the framework exits Play and removes its bootstrap scene.
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isUpdating &&
                    !EditorApplication.isCompiling && EditorApplication.timeSinceStartup >= SessionState.GetFloat(BatchExit + ".After", 0))
                {
                    SessionState.SetBool(BatchExit, false);
                    EditorApplication.Exit(SessionState.GetInt(BatchExit + ".Code", 1));
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Active, false) ||
                !File.Exists(Request)) return;
            var command = File.ReadAllText(Request).Trim();
            if (command != "automated" && command != "hardware" && command != "reentry") return;
            File.WriteAllText(Request, "accepted " + command);
            try
            {
                if (command == "automated") RunAutomated();
                else
                {
                    Status("Starting real hardware probe; no simulated provider is used.");
                    if (command == "reentry") QuestLANLinkProbe.BeginReentry();
                    else QuestLANLinkProbe.Begin();
                }
            }
            catch (Exception exception)
            {
                RestoreStartup();
                Status("Failed to start: " + exception);
                Debug.LogException(exception);
            }
        }

        [MenuItem("QuestLAN/Run isolated XR acceptance tests")]
        public static void RunAutomated()
        {
            RunAutomatedInternal(false);
        }

        public static void RunAllAutomatedBatch()
        {
            RunAutomatedInternal(true);
        }

        private static void RunAutomatedInternal(bool all)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Active, false))
                throw new InvalidOperationException("Stop Play Mode or the existing test run first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (all && Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (!settings) throw new InvalidOperationException("Standalone XR settings missing.");
            SessionState.SetBool(OriginalStartup, settings.InitManagerOnStart);
            SessionState.SetBool(Active, true);
            SessionState.SetBool(BatchAll, all);
            // In-memory only; do not persist the disabled value to project assets.
            settings.InitManagerOnStart = false;
            Directory.CreateDirectory("Logs");
            Status("Running isolated PlayMode tests; hardware XR startup temporarily disabled.");
            try
            {
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.PlayMode, assemblyNames = all ?
                        new[] { "QuestLAN.Tests", "QuestLAN.Networking.Tests", "QuestLAN.Simulation.Tests" } : new[] { "QuestLAN.Tests" }
                }));
            }
            catch
            {
                RestoreStartup();
                throw;
            }
        }

        [MenuItem("QuestLAN/Restore XR startup after interrupted tests")]
        public static void RestoreStartup()
        {
            if (!SessionState.GetBool(Active, false)) return;
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings)
                settings.InitManagerOnStart = SessionState.GetBool(OriginalStartup, true);
            SessionState.SetBool(Active, false);
        }

        private static void Status(string value)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/QuestLAN-Acceptance-State.txt", value);
            Debug.Log("QuestLAN acceptance: " + value);
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Active, false)) return;
                bool all = SessionState.GetBool(BatchAll, false);
                try
                {
                    TestRunnerApi.SaveResultToFile(result, all ? "Logs/M3-PlayMode-Final.xml" : "Logs/QuestLAN-XRAcceptance.xml");
                }
                finally { RestoreStartup(); }
                Status(result.ResultState + "; passed=" + result.PassCount + "; failed=" + result.FailCount +
                    "; skipped=" + result.SkipCount + ". XR startup restored.");
                SessionState.SetBool(BatchAll, false);
                if (all && Application.isBatchMode)
                {
                    SessionState.SetInt(BatchExit + ".Code", result.FailCount == 0 && result.SkipCount == 0 ? 0 : 1);
                    SessionState.SetFloat(BatchExit + ".After", (float)EditorApplication.timeSinceStartup + 2);
                    SessionState.SetBool(BatchExit, true);
                }
            }
        }
    }
}
