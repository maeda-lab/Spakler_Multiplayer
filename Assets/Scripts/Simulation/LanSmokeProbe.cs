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
            public string role, address, machineName, startedUtc;
            public int port;
            public bool connected, sawTwoPlayers, sawRemoteTracked, sawRemoteMovement;
            public bool sawRemoteSingleHandLost, sawRemoteAllLost, sawRemoteRecovery;
            public bool remoteOwnershipRejected, rootReconstructionCorrect, sawRemoteLeave, sawRemoteRejoin;
            public int maxReceived, connectionCount;
            public bool rttMeasured, sawRemoteColor, lateJoinColor;
            public double currentRttMs, smoothedRttMs;
            public int rttSamples;
            public string finalStatus;
            public List<string> events = new List<string>();
        }
        private Report report;
        private LanSessionController session;
        private MockPoseSource source;
        private string role, reportPath;
        private double started, duration;
        private double peerStarted = double.NaN;
        private int previousPlayers;
        private bool hadRemote, lastRemoteLost;
        private Vector3 previousRemoteHand;
        private bool hasRemoteHand;
        private bool finished;
        private bool colorRequested;

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
            string address = Argument(args, "--lan-address") ?? "127.0.0.1";
            report = new Report { role = role, address = address, port = port,
                machineName = SystemInfo.deviceName, startedUtc = DateTime.UtcNow.ToString("O") };
            started = Time.realtimeSinceStartupAsDouble;
            if (role == "host") session.StartHost("127.0.0.1", port);
            else session.StartClient(address, port);
            WriteReport();
        }

        private void Update()
        {
            if (report == null || finished) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            // Start the tracking scenario when both peers exist, so a manually launched
            // LAN client can join later without missing the Host's loss/recovery window.
            double scenarioTime = double.IsNaN(peerStarted) ? 0 : elapsed - peerStarted;
            source.LeftHandTracked = role != "host" || scenarioTime < 4 || scenarioTime > 7;
            source.RightHandTracked = role == "host" || scenarioTime < 4 || scenarioTime > 7;
            source.OutputAvailable = scenarioTime < 9 || scenarioTime > 11;
            var manager = GetComponent<NetworkManager>();
            var players = FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var player in players)
            {
                if (!player.IsSpawned) continue;
                count++;
                if (player.IsOwner)
                {
                    report.connected = true;
                    var localSignals = player.GetComponent<NetworkPlayerSignals>();
                    if (localSignals)
                    {
                        if (!colorRequested && elapsed >= 2) { colorRequested = true; localSignals.RequestColorChange(); }
                        report.rttMeasured |= localSignals.Rtt.HasSample;
                        report.currentRttMs = localSignals.Rtt.CurrentMilliseconds;
                        report.smoothedRttMs = localSignals.Rtt.SmoothedMilliseconds;
                        report.rttSamples = localSignals.Rtt.SampleCount;
                    }
                    continue;
                }
                var remoteSignals = player.GetComponent<NetworkPlayerSignals>();
                if (remoteSignals && remoteSignals.ColorIndex.Value == 1)
                {
                    report.sawRemoteColor = true;
                    if (elapsed < 2) report.lateJoinColor = true;
                }
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
            if (count >= 2)
            {
                report.sawTwoPlayers = true;
                if (double.IsNaN(peerStarted)) peerStarted = elapsed;
            }
            else peerStarted = double.NaN;
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
