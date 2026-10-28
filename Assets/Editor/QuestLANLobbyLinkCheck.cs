using System;
using System.IO;
using QuestLAN.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuestLAN.Editor
{
    [InitializeOnLoad]
    public static class QuestLANLobbyLinkCheck
    {
        private const string Request = "Logs/Lobby-Link-Request.txt";
        private const string Scheduled = "QuestLAN.LobbyLinkCheck.Scheduled";
        private static double nextPoll;
        static QuestLANLobbyLinkCheck()
        {
            EditorApplication.update += () =>
            {
                if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 1;
                if (!File.Exists(Request)) return;
                var command = File.ReadAllText(Request).Trim();
                if (command == "quit")
                {
                    File.WriteAllText(Request, "accepted quit");
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorApplication.Exit(0);
                }
                else if (command == "setup-start")
                {
                    File.WriteAllText(Request, "accepted setup");
                    QuestLANIntegrationSetup.SetupLocomotionLandmarksBatch();
                    Begin();
                }
                else if (command == "start") Begin();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Scheduled, false)) return;
                SessionState.SetBool(Scheduled, false);
                string path = Path.GetFullPath("Logs/Lobby-Link-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                new GameObject("Real Link observation").AddComponent<LobbyLinkCheck>().Configure(path, true);
                File.WriteAllText(Request, "running " + path);
            };
        }
        [MenuItem("QuestLAN/Run desktop lobby and locomotion Link check")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/DesktopLobby.unity");
            SessionState.SetBool(Scheduled, true);
            Directory.CreateDirectory("Logs"); File.WriteAllText(Request, "accepted");
            EditorApplication.isPlaying = true;
        }
    }
}
