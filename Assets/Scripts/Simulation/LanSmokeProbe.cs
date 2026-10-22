using System;
using System.Collections.Generic;
using System.IO;
using QuestLAN.Networking;
using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Simulation
{
    /// <summary>Opt-in real-process LAN checks; inactive without --lan-role command-line argument.</summary>
    [DefaultExecutionOrder(500)] // Observe visuals after AvatarPoseView.LateUpdate.
    public sealed class LanSmokeProbe : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public int schemaVersion = 2;
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
            public bool completedNormally;
            public double elapsedSeconds, requestedDurationSeconds, connectedSeconds, maxFrameStepSeconds;
            public int observedSentSnapshots, observedReceivedSnapshots, receiveTimeoutCount, timeoutRecoveries;
            public int timeoutVisualChecks, timeoutHiddenSamples, timeoutVisibleViolations, timeoutInvalidPoseViolations;
            public double maxReceiveAgeSeconds, longestReceiveTimeoutSeconds;
            public long peakWorkingSetBytes, finalWorkingSetBytes, peakPrivateBytes, finalPrivateBytes;
            public long finalManagedBytes, finalUnityAllocatedBytes;
            public double processCpuSeconds;
            public int metricSamples, eventsOmitted;
            public int headlessTargetFrameRate;
            public string metricsError;
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
        private StreamWriter metrics;
        private FileStream outputLease;
        private bool initialized;
        private double nextMetrics, nextReport, timeoutStarted;
        private NetworkPoseSync observedLocal, observedRemote;
        private int previousSent, previousReceived;
        private bool receiveTimedOut;
        private bool limitedFrameRate;
        private int originalFrameRate, originalVSync;

        private void Start()
        {
            try { Initialize(); }
            catch (Exception error)
            {
                finished = true; enabled = false;
                metrics?.Dispose(); metrics = null;
                outputLease?.Dispose(); outputLease = null;
                Debug.LogError("LAN probe initialization failed; choose a new report filename and preserve existing outputs. " + error);
                Application.Quit(2);
            }
        }

        private void Initialize()
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
                machineName = SystemInfo.deviceName, startedUtc = DateTime.UtcNow.ToString("O"),
                requestedDurationSeconds = duration };
            started = Time.realtimeSinceStartupAsDouble;
            if (Application.isBatchMode)
            {
                originalFrameRate = Application.targetFrameRate; originalVSync = QualitySettings.vSyncCount;
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
                limitedFrameRate = true; report.headlessTargetFrameRate = 60;
            }
            outputLease = ReserveReportOutput(reportPath);
            metrics = new StreamWriter(new FileStream(reportPath + ".metrics.csv", FileMode.CreateNew,
                FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            metrics.WriteLine("elapsed_seconds,players,sent_snapshots,received_snapshots,receive_age_seconds,receive_timed_out,rtt_ms,smoothed_rtt_ms,rtt_timed_out,working_set_bytes,private_bytes,managed_bytes,unity_allocated_bytes,cpu_seconds,max_frame_step_seconds");
            if (role == "host") session.StartHost("127.0.0.1", port);
            else session.StartClient(address, port);
            WriteReport();
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || report == null || finished) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            report.elapsedSeconds = elapsed;
            report.maxFrameStepSeconds = Math.Max(report.maxFrameStepSeconds, Time.unscaledDeltaTime);
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
                    if (observedLocal != player) { observedLocal = player; previousSent = 0; }
                    report.observedSentSnapshots += Math.Max(0, player.SentCount - previousSent);
                    previousSent = player.SentCount;
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
                if (observedRemote != player)
                {
                    observedRemote = player; previousReceived = 0; receiveTimedOut = false;
                }
                report.observedReceivedSnapshots += Math.Max(0, player.ReceivedCount - previousReceived);
                previousReceived = player.ReceivedCount;
                double age = player.ReceiveAgeSeconds;
                if (!double.IsInfinity(age)) report.maxReceiveAgeSeconds = Math.Max(report.maxReceiveAgeSeconds, age);
                if (player.ReceiveTimedOut && !receiveTimedOut)
                {
                    receiveTimedOut = true; timeoutStarted = elapsed; report.receiveTimeoutCount++;
                    Event($"{elapsed:F2}s pose receive timeout");
                }
                if (receiveTimedOut)
                    report.longestReceiveTimeoutSeconds = Math.Max(report.longestReceiveTimeoutSeconds, elapsed - timeoutStarted);
                if (!player.ReceiveTimedOut && receiveTimedOut)
                {
                    receiveTimedOut = false; report.timeoutRecoveries++;
                    Event($"{elapsed:F2}s pose receive recovered");
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
                report.connectedSeconds += Time.unscaledDeltaTime;
                report.sawTwoPlayers = true;
                if (double.IsNaN(peerStarted)) peerStarted = elapsed;
            }
            else peerStarted = double.NaN;
            if (count != previousPlayers)
            {
                Event($"{elapsed:F2}s players={count}");
                if (count >= 2) { report.connectionCount++; if (hadRemote) report.sawRemoteRejoin = true; hadRemote = true; }
                if (previousPlayers >= 2 && count < 2) report.sawRemoteLeave = true;
                previousPlayers = count;
                WriteReport();
            }
            if (count < 2) { observedRemote = null; receiveTimedOut = false; }
            if (elapsed >= nextMetrics) { nextMetrics = elapsed + 1; SampleMetrics(elapsed, count); }
            if (elapsed >= nextReport) { nextReport = elapsed + 5; WriteReport(); }
            if (elapsed >= duration)
            {
                report.finalStatus = session.Status;
                report.completedNormally = true;
                WriteReport();
                metrics?.Dispose(); metrics = null;
                outputLease?.Dispose(); outputLease = null;
                finished = true;
                session.Disconnect();
                Invoke(nameof(Quit), 0.5f);
            }
        }

        private void LateUpdate()
        {
            if (!initialized || finished || !observedRemote || !observedRemote.ReceiveTimedOut) return;
            if (observedRemote.TryGetLatestPose(out var frame) &&
                (frame.HeadTracked || frame.LeftHandTracked || frame.RightHandTracked)) report.timeoutInvalidPoseViolations++;
            var head = observedRemote.transform.Find("Head");
            var left = observedRemote.transform.Find("Left wrist (cube)");
            var right = observedRemote.transform.Find("Right wrist (capsule)");
            if (!head || !left || !right) return;
            report.timeoutVisualChecks++;
            if (!head.gameObject.activeSelf && !left.gameObject.activeSelf && !right.gameObject.activeSelf)
                report.timeoutHiddenSamples++;
            else report.timeoutVisibleViolations++;
        }

        public static FileStream ReserveReportOutput(string path)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            // Exclusive per-run lease closes the check/open race between two processes.
            var lease = new FileStream(fullPath + ".run.lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            try
            {
                if (File.Exists(fullPath) || File.Exists(fullPath + ".metrics.csv"))
                    throw new IOException("Report or metrics already exists: " + fullPath);
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }

        private void Event(string value)
        {
            if (report.events.Count < 256) report.events.Add(value);
            else report.eventsOmitted++;
        }

        private void SampleMetrics(double elapsed, int players)
        {
            try
            {
                report.finalStatus = session.Status;
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                process.Refresh();
                ReadProcessMemory(out report.finalWorkingSetBytes, out report.finalPrivateBytes);
                report.peakWorkingSetBytes = Math.Max(report.peakWorkingSetBytes, report.finalWorkingSetBytes);
                report.peakPrivateBytes = Math.Max(report.peakPrivateBytes, report.finalPrivateBytes);
                report.finalManagedBytes = GC.GetTotalMemory(false);
                report.finalUnityAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                report.processCpuSeconds = process.TotalProcessorTime.TotalSeconds;
                report.metricSamples++;
                double age = observedRemote ? observedRemote.ReceiveAgeSeconds : -1;
                if (double.IsInfinity(age)) age = -1;
                var signals = observedLocal ? observedLocal.GetComponent<NetworkPlayerSignals>() : null;
                metrics.WriteLine(FormattableString.Invariant($"{elapsed:F3},{players},{report.observedSentSnapshots},{report.observedReceivedSnapshots},{age:F4},{receiveTimedOut},{report.currentRttMs:F3},{report.smoothedRttMs:F3},{(signals && signals.Rtt.TimedOut)},{report.finalWorkingSetBytes},{report.finalPrivateBytes},{report.finalManagedBytes},{report.finalUnityAllocatedBytes},{report.processCpuSeconds:F3},{report.maxFrameStepSeconds:F4}"));
            }
            catch (Exception error)
            {
                if (report.metricsError == null) { report.metricsError = error.Message; Debug.LogException(error); }
            }
        }

        public static void ReadProcessMemory(out long workingSet, out long privateBytes)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Unity's Windows Mono Process properties can silently return zero.
            uint size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<ProcessMemoryCounters>();
            if (!GetProcessMemoryInfo(CurrentProcessHandle(), out var counters, size))
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            workingSet = (long)counters.WorkingSetSize.ToUInt64();
            privateBytes = (long)counters.PrivateUsage.ToUInt64();
#else
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            process.Refresh(); workingSet = process.WorkingSet64; privateBytes = process.PrivateMemorySize64;
#endif
            if (workingSet <= 0 || privateBytes <= 0)
                throw new InvalidOperationException("Process memory counters unavailable; zero is not a valid measurement.");
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint Size, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
        private static extern IntPtr CurrentProcessHandle();
        [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);
#endif

        private void OnDestroy()
        {
            metrics?.Dispose(); metrics = null;
            outputLease?.Dispose(); outputLease = null;
            if (limitedFrameRate) { Application.targetFrameRate = originalFrameRate; QualitySettings.vSyncCount = originalVSync; }
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
