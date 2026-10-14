using System;
using System.Globalization;
using System.IO;
using QuestLAN.Shared;
using QuestLAN.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuestLAN.Editor
{
    /// <summary>Explicit, interactive 90-second hardware probe; does not simulate tracking.</summary>
    [InitializeOnLoad]
    public static class QuestLANLinkProbe
    {
        private const string Running = "QuestLAN.LinkProbe.Running";
        private const string Csv = "Logs/QuestLAN-LinkProbe.csv";
        private static double started;
        private static double nextSample;
        private static int samples;
        private static int heads;
        private static int lefts;
        private static int rights;
        private static Vector3 headMin = Vector3.positiveInfinity;
        private static Vector3 headMax = Vector3.negativeInfinity;

        static QuestLANLinkProbe()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Running, false))
                    Finish("Play Mode stopped by user or Editor.");
            };
        }

        [MenuItem("QuestLAN/Run 90-second Link hardware probe")]
        public static void Begin()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("Switch to Windows x64 before the Link probe.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before starting the probe.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Csv, "localSeconds,sequence,headTracked,leftTracked,rightTracked,headX,headY,headZ\n");
            started = nextSample = 0;
            samples = heads = lefts = rights = 0;
            headMin = Vector3.positiveInfinity;
            headMax = Vector3.negativeInfinity;
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            var now = EditorApplication.timeSinceStartup;
            if (started == 0) started = now;
            if (now < nextSample) return;
            nextSample = now + 0.25;
            var source = UnityEngine.Object.FindFirstObjectByType<XRHandsPoseSource>();
            if (source && source.TryGetLatestPose(out AvatarPoseFrame frame))
            {
                ++samples;
                if (frame.HeadTracked)
                {
                    ++heads;
                    headMin = Vector3.Min(headMin, frame.Head.position);
                    headMax = Vector3.Max(headMax, frame.Head.position);
                }
                if (frame.LeftHandTracked) ++lefts;
                if (frame.RightHandTracked) ++rights;
                File.AppendAllText(Csv, string.Format(CultureInfo.InvariantCulture,
                    "{0:F3},{1},{2},{3},{4},{5:F4},{6:F4},{7:F4}\n", now - started, frame.Sequence,
                    frame.HeadTracked, frame.LeftHandTracked, frame.RightHandTracked,
                    frame.Head.position.x, frame.Head.position.y, frame.Head.position.z));
            }
            if (now - started < 90) return;
            Finish("90-second observation completed.");
            EditorApplication.ExitPlaymode();
        }

        private static void Finish(string reason)
        {
            SessionState.SetBool(Running, false);
            var summary = reason + "\nSamples=" + samples + "; head=" + heads + "; left=" + lefts +
                "; right=" + rights + "\nHead position range in AvatarRoot meters=" +
                (heads > 0 ? (headMax - headMin).ToString("F3") : "No valid head samples") +
                "\nDevice model and visual correctness require operator confirmation. " +
                "Zero valid hands does not establish whether the cause is runtime support, configuration, or tracking conditions.";
            File.WriteAllText("Logs/QuestLAN-LinkProbe-Summary.txt", summary);
            Debug.Log(summary);
        }
    }
}
