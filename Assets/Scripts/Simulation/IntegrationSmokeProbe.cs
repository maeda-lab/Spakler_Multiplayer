using System;
using System.IO;
using QuestLAN.Integration;
using QuestLAN.Networking;
using QuestLAN.XR;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace QuestLAN.Simulation
{
    /// <summary>Explicit process-test arguments only. Never injects fake tracking into MainScene.</summary>
    public sealed class IntegrationSmokeProbe : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public string role, finalStatus, address, machineName, startedUtc;
            public int port;
            public bool connected, mapped, mappingCorrect, oneHardwareRig, sourceBound;
            public bool sawRemote, remoteHasNoRig, remoteInvalid, colorReplicated, rttMeasured;
            public bool mappingCleared, stoppedCleanly;
            public int received, rttSamples;
            public int colorRequests;
            public byte localColorIndex, remoteColorIndex, localRenderedColorIndex;
            public string colorShader;
            public int remoteHeadSamples, remoteLeftSamples, remoteRightSamples, remoteLeftLostOnly, remoteRightLostOnly;
            public double rttMs;
        }
        private Report report;
        private LanSessionController session;
        private XRNetworkBinding binding;
        private NetworkManager manager;
        private double started, duration;
        private string path;
        private bool colorRequested, stopping;
        private uint lastSequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRequestedProbe()
        {
            var args = Environment.GetCommandLineArgs();
            string role = Argument(args, "--m3-role");
            if (string.IsNullOrEmpty(role)) return;
            var session = FindFirstObjectByType<XRNetworkBinding>();
            if (!session) throw new InvalidOperationException("MainScene integration missing.");
            var probe = session.gameObject.AddComponent<IntegrationSmokeProbe>();
            probe.report = new Report { role = role };
            probe.path = Argument(args, "--m3-report");
            if (string.IsNullOrEmpty(probe.path)) throw new InvalidOperationException("--m3-report is required.");
            if (!double.TryParse(Argument(args, "--m3-duration"), out probe.duration)) probe.duration = 18;
        }

        private void Start()
        {
            session = GetComponent<LanSessionController>(); manager = GetComponent<NetworkManager>();
            binding = GetComponent<XRNetworkBinding>();
            started = Time.realtimeSinceStartupAsDouble;
            var args = Environment.GetCommandLineArgs();
            if (!ushort.TryParse(Argument(args, "--m3-port"), out ushort port)) port = 17779;
            report.address = Argument(args, "--m3-address") ?? "127.0.0.1";
            report.port = port;
            report.machineName = SystemInfo.deviceName;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            if (report.role == "host") session.StartHost("127.0.0.1", port);
            else session.StartClient(report.address, port);
        }

        private void Update()
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            if (stopping)
            {
                if (elapsed < duration + 1) return;
                report.mappingCleared = !binding.IsMapped;
                report.stoppedCleanly = !manager.IsListening && FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None).Length == 0;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                enabled = false;
#if UNITY_EDITOR
                if (Application.isEditor) UnityEditor.EditorApplication.isPlaying = false;
                else Application.Quit();
#else
                Application.Quit();
#endif
                return;
            }
            report.oneHardwareRig = FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length == 1;
            foreach (var player in FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned) continue;
                var signals = player.GetComponent<NetworkPlayerSignals>();
                if (player.IsOwner)
                {
                    report.connected = true;
                    report.mapped |= binding.IsMapped;
                    report.sourceBound |= session.LocalPoseSource is XRHandsPoseSource;
                    var localRoot = GameObject.Find("Local AvatarRoot").transform;
                    report.mappingCorrect |= Vector3.Distance(localRoot.position, player.transform.position) < 0.0001f &&
                        Quaternion.Angle(localRoot.rotation, player.transform.rotation) < 0.001f;
                    if (!colorRequested && elapsed > 2) { colorRequested = true; signals.RequestColorChange(); }
                    report.rttMeasured |= signals.Rtt.HasSample;
                    report.rttSamples = signals.Rtt.SampleCount; report.rttMs = signals.Rtt.SmoothedMilliseconds;
                    report.colorRequests = signals.ColorRequestsSent; report.localColorIndex = signals.ColorIndex.Value;
                    var view = player.GetComponent<NetworkAvatarPresentation>();
                    if (view)
                    {
                        report.localRenderedColorIndex = view.AppliedColorIndex;
                        report.colorShader = view.PulseMaterial ? view.PulseMaterial.shader.name : "missing";
                    }
                }
                else
                {
                    report.sawRemote = true;
                    report.remoteHasNoRig = player.GetComponentsInChildren<XROrigin>(true).Length == 0 && player.GetComponentsInChildren<Camera>(true).Length == 0;
                    report.received = Math.Max(report.received, player.ReceivedCount);
                    report.colorReplicated |= signals.ColorIndex.Value == 1;
                    report.remoteColorIndex = signals.ColorIndex.Value;
                    if (player.TryGetLatestPose(out var frame))
                    {
                        report.remoteInvalid |= !frame.HeadTracked && !frame.LeftHandTracked && !frame.RightHandTracked;
                        if (frame.Sequence != lastSequence)
                        {
                            lastSequence = frame.Sequence;
                            if (frame.HeadTracked) report.remoteHeadSamples++;
                            if (frame.LeftHandTracked) report.remoteLeftSamples++;
                            if (frame.RightHandTracked) report.remoteRightSamples++;
                            if (!frame.LeftHandTracked && frame.RightHandTracked) report.remoteLeftLostOnly++;
                            if (frame.LeftHandTracked && !frame.RightHandTracked) report.remoteRightLostOnly++;
                        }
                    }
                }
            }
            if (elapsed < duration) return;
            report.finalStatus = session.Status;
            session.Disconnect(); stopping = true;
        }

        private static string Argument(string[] args, string key)
        { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    }
}
