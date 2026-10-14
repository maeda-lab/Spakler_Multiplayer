using System;
using System.Collections.Generic;
using System.IO;
using QuestLAN.Networking;
using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Simulation
{
    /// <summary>Opt-in real-process LAN checks; inactive without --lan-role command-line argument.</summary>
    public sealed class LanSmokeProbe : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public string role;
            public bool connected, sawTwoPlayers, sawRemoteTracked, sawRemoteMovement;
            public bool sawRemoteSingleHandLost, sawRemoteAllLost, sawRemoteRecovery;
            public bool remoteOwnershipRejected, rootReconstructionCorrect, sawRemoteLeave, sawRemoteRejoin;
            public int maxReceived, connectionCount;
            public string finalStatus;
            public List<string> events = new List<string>();
        }
        private Report report;
        private LanSessionController session;
        private MockPoseSource source;
        private string role, reportPath;
        private double started, duration;
        private int previousPlayers;
        private bool hadRemote, lastRemoteLost;
        private Vector3 previousRemoteHand;
        private bool hasRemoteHand;
        private bool finished;

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            role = Argument(args, "--lan-role");
            if (string.IsNullOrEmpty(role)) { enabled = false; return; }
            reportPath = Argument(args, "--lan-report");
            if (string.IsNullOrEmpty(reportPath)) throw new InvalidOperationException("Smoke run requires --lan-report");
            if (!double.TryParse(Argument(args, "--lan-duration"), out duration)) duration = 20;
            if (!ushort.TryParse(Argument(args, "--lan-port"), out var port)) port = 7777;
            session = GetComponent<LanSessionController>();
            source = GetComponent<MockPoseSource>();
            report = new Report { role = role };
            started = Time.realtimeSinceStartupAsDouble;
            if (role == "host") session.StartHost("127.0.0.1", port);
            else session.StartClient("127.0.0.1", port);
            WriteReport();
        }

        private void Update()
        {
            if (report == null || finished) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            source.LeftHandTracked = role != "host" || elapsed < 4 || elapsed > 7;
            source.RightHandTracked = role == "host" || elapsed < 4 || elapsed > 7;
            source.OutputAvailable = elapsed < 9 || elapsed > 11;
            var manager = GetComponent<NetworkManager>();
            var players = FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var player in players)
            {
                if (!player.IsSpawned) continue;
                count++;
                if (player.IsOwner) { report.connected = true; continue; }
                report.maxReceived = Math.Max(report.maxReceived, player.ReceivedCount);
                if (!report.remoteOwnershipRejected)
                {
                    try { player.BindLocalSource(source); }
                    catch (InvalidOperationException) { report.remoteOwnershipRejected = true; }
                }
                if (!player.TryGetLatestPose(out var frame)) continue;
                bool allTracked = frame.HeadTracked && frame.LeftHandTracked && frame.RightHandTracked;
                if (allTracked) report.sawRemoteTracked = true;
                if (frame.HeadTracked && frame.LeftHandTracked != frame.RightHandTracked) report.sawRemoteSingleHandLost = true;
                if (!frame.HeadTracked && !frame.LeftHandTracked && !frame.RightHandTracked)
                { report.sawRemoteAllLost = true; lastRemoteLost = true; }
                if (allTracked && lastRemoteLost) report.sawRemoteRecovery = true;
                if (frame.RightHandTracked)
                {
                    if (hasRemoteHand && Vector3.Distance(previousRemoteHand, frame.RightHand.position) > 0.005f)
                        report.sawRemoteMovement = true;
                    previousRemoteHand = frame.RightHand.position; hasRemoteHand = true;
                    var visual = player.transform.Find("Right wrist (capsule)");
                    if (visual && visual.gameObject.activeSelf && Vector3.Distance(visual.position, player.transform.TransformPoint(frame.RightHand.position)) < 0.03f)
                        report.rootReconstructionCorrect = true;
                }
            }
            if (count >= 2) report.sawTwoPlayers = true;
            if (count != previousPlayers)
            {
                report.events.Add($"{elapsed:F2}s players={count}");
                if (count >= 2) { report.connectionCount++; if (hadRemote) report.sawRemoteRejoin = true; hadRemote = true; }
                if (previousPlayers >= 2 && count < 2) report.sawRemoteLeave = true;
                previousPlayers = count;
                WriteReport();
            }
            if (elapsed >= duration)
            {
                report.finalStatus = session.Status;
                WriteReport();
                finished = true;
                session.Disconnect();
                Invoke(nameof(Quit), 0.5f);
            }
        }

        private void Quit() => Application.Quit();
        private void WriteReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }
        private static string Argument(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
