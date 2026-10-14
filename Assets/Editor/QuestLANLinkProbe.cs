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
        private static bool sawBoth;
        private static int leftLoss;
        private static int rightLoss;
        private static int leftRecovery;
        private static int rightRecovery;
        private static int phase = -1;
        private static TextMesh instruction;
        private static readonly string[] Instructions =
        {
            "BOTH HANDS VISIBLE", "HIDE LEFT / KEEP RIGHT VISIBLE", "RESTORE BOTH HANDS",
            "HIDE RIGHT / KEEP LEFT VISIBLE", "RESTORE BOTH HANDS", "KEEP HANDS VISIBLE / MOVE HEAD"
        };

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
            BeginRun(90, true);
        }

        [MenuItem("QuestLAN/Run 30-second Link reentry probe")]
        public static void BeginReentry()
        {
            BeginRun(30, false);
        }

        private static void BeginRun(float duration, bool staged)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("Switch to Windows x64 before the Link probe.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before starting the probe.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            Directory.CreateDirectory("Logs");
            var runDirectory = "Logs/QuestLAN-LinkRuns/" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(runDirectory);
            SessionState.SetString("QuestLAN.LinkProbe.Directory", runDirectory);
            SessionState.SetFloat("QuestLAN.LinkProbe.Duration", duration);
            SessionState.SetBool("QuestLAN.LinkProbe.Staged", staged);
            File.WriteAllText(Csv, "localSeconds,phase,available,sequence,headTracked,leftTracked,rightTracked," +
                "headX,headY,headZ,headQx,headQy,headQz,headQw,leftX,leftY,leftZ,leftQx,leftQy,leftQz,leftQw," +
                "rightX,rightY,rightZ,rightQx,rightQy,rightQz,rightQw\n");
            started = nextSample = 0;
            samples = heads = lefts = rights = 0;
            sawBoth = false;
            leftLoss = rightLoss = leftRecovery = rightRecovery = 0;
            phase = -1;
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
            nextSample = now + 0.1;
            var elapsed = now - started;
            var staged = SessionState.GetBool("QuestLAN.LinkProbe.Staged", true);
            var duration = SessionState.GetFloat("QuestLAN.LinkProbe.Duration", 90);
            var currentPhase = staged ? Math.Min(5, (int)(elapsed / 15)) : 0;
            if (phase != currentPhase)
            {
                phase = currentPhase;
                Debug.Log("QuestLAN hand acceptance: " + Instructions[phase]);
            }
            var source = UnityEngine.Object.FindFirstObjectByType<XRHandsPoseSource>();
            var frame = default(AvatarPoseFrame);
            var available = source && source.TryGetLatestPose(out frame);
            if (available)
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
                sawBoth |= frame.LeftHandTracked && frame.RightHandTracked;
                if (sawBoth && phase == 1 && !frame.LeftHandTracked && frame.RightHandTracked) ++leftLoss;
                if (leftLoss >= 3 && phase == 2 && frame.LeftHandTracked && frame.RightHandTracked) ++leftRecovery;
                if (sawBoth && phase == 3 && !frame.RightHandTracked && frame.LeftHandTracked) ++rightLoss;
                if (rightLoss >= 3 && phase == 4 && frame.LeftHandTracked && frame.RightHandTracked) ++rightRecovery;
            }
            File.AppendAllText(Csv, FormattableString.Invariant(
                $"{elapsed:F3},{phase},{available},{frame.Sequence},{frame.HeadTracked},{frame.LeftHandTracked},{frame.RightHandTracked},") +
                PoseCsv(frame.Head, available && frame.HeadTracked) + "," +
                PoseCsv(frame.LeftHand, available && frame.LeftHandTracked) + "," +
                PoseCsv(frame.RightHand, available && frame.RightHandTracked) + "\n");
            var remaining = staged ? 15 - (int)(elapsed % 15) : Math.Max(0, (int)Math.Ceiling(duration - elapsed));
            ShowInstruction((staged ? Instructions[phase] : "PLAY REENTRY / BOTH HANDS VISIBLE") + "\n" + remaining + "s   L:" +
                (available && frame.LeftHandTracked ? "OK" : "LOST") + "  R:" +
                (available && frame.RightHandTracked ? "OK" : "LOST"));
            if (now - started < duration) return;
            Finish(duration + "-second observation completed.");
            EditorApplication.ExitPlaymode();
        }

        private static string PoseCsv(Pose pose, bool valid)
        {
            if (!valid) return "NaN,NaN,NaN,NaN,NaN,NaN,NaN";
            return string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4},{3:F5},{4:F5},{5:F5},{6:F5}",
                pose.position.x, pose.position.y, pose.position.z, pose.rotation.x, pose.rotation.y,
                pose.rotation.z, pose.rotation.w);
        }

        private static void ShowInstruction(string value)
        {
            if (!instruction && Camera.main)
            {
                var display = new GameObject("QuestLAN temporary acceptance instructions");
                display.transform.SetParent(Camera.main.transform, false);
                display.transform.localPosition = new Vector3(0, -0.15f, 0.7f);
                instruction = display.AddComponent<TextMesh>();
                instruction.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                instruction.fontSize = 48;
                instruction.characterSize = 0.008f;
                instruction.anchor = TextAnchor.MiddleCenter;
                instruction.alignment = TextAlignment.Center;
                instruction.color = Color.white;
                if (instruction.font) display.GetComponent<MeshRenderer>().sharedMaterial = instruction.font.material;
            }
            if (instruction) instruction.text = value;
        }

        private static void Finish(string reason)
        {
            SessionState.SetBool(Running, false);
            var summary = reason + "\nSamples=" + samples + "; head=" + heads + "; left=" + lefts +
                "; right=" + rights + "\nHead position range in AvatarRoot meters=" +
                (heads > 0 ? (headMax - headMin).ToString("F3") : "No valid head samples") +
                "\nStaged occlusion test=" + SessionState.GetBool("QuestLAN.LinkProbe.Staged", true) +
                "\nLeft independent loss samples=" + leftLoss + "; recovery samples=" + leftRecovery +
                "; evidence=" + (leftLoss >= 3 && leftRecovery >= 3) +
                "\nRight independent loss samples=" + rightLoss + "; recovery samples=" + rightRecovery +
                "; evidence=" + (rightLoss >= 3 && rightRecovery >= 3) +
                "\nDevice model and visual correctness require operator confirmation. " +
                "Zero valid hands does not establish whether the cause is runtime support, configuration, or tracking conditions.";
            File.WriteAllText("Logs/QuestLAN-LinkProbe-Summary.txt", summary);
            var runDirectory = SessionState.GetString("QuestLAN.LinkProbe.Directory", "");
            if (!string.IsNullOrEmpty(runDirectory))
            {
                File.Copy(Csv, Path.Combine(runDirectory, "frames.csv"), true);
                File.WriteAllText(Path.Combine(runDirectory, "summary.txt"), summary);
            }
            if (instruction) UnityEngine.Object.Destroy(instruction.gameObject);
            Debug.Log(summary);
        }
    }
}
