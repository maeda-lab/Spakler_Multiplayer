using System;
using System.IO;
using QuestLAN.Avatar;
using QuestLAN.Integration;
using QuestLAN.Networking;
using QuestLAN.Shared;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace QuestLAN.Simulation
{
    /// <summary>Explicit batch-only synthetic peer. Never used by normal DesktopLobby/MainScene.</summary>
    [DefaultExecutionOrder(-110)]
    public sealed class LobbyFlowProbe : MonoBehaviour, IAvatarPoseSource, IHandJointSource
    {
        [Serializable] private sealed class Report
        {
            public string role, status;
            public bool desktopBeforeVR, invalidPortRejected, roomEntered, sawTwoPlayers, syntheticPoses;
            public bool localHandsVisible, remoteHandsAndHeadVisible, sawIndependentLoss, sawRecovery;
            public bool receivedBeyondFiveMeters, returnedToLobby, xrStopped, zeroPlayers, oneRig;
            public int received, roomEntryCount;
            public bool repeatEntryClean, localJointSkeleton, remoteJointSkeleton, independentJointLoss, jointRecovery, remoteFingerArticulation;
            public int receivedJointPackets;
        }
        private Report report;
        private string path, role;
        private double started, entered = -1, duration;
        private bool clicked, bound, stopping, repeated;
        private uint sequence;
        private bool fingerSet; private float firstFinger;
        private AvatarPoseFrame frame;
        private FileStream lease;
        private readonly Pose[] leftJoints = new Pose[26], rightJoints = new Pose[26];
        public bool TryGetLatestHandJoints(out HandJointFrame value)
        {
            for (int i = 0; i < 26; i++)
            {
                float curl = Mathf.Sin(Time.time * 2) * 0.025f;
                var pose = new Pose(new Vector3(((i - 2) / 5 - 2) * 0.02f, i < 2 ? 0 : curl, i < 2 ? 0 : 0.03f + ((i - 2) % 5) * 0.02f), Quaternion.identity);
                leftJoints[i] = rightJoints[i] = pose;
            }
            value = new HandJointFrame { Sequence = frame.Sequence, LocalCaptureTimeSeconds = frame.LocalCaptureTimeSeconds,
                LeftWrist = frame.LeftHand, RightWrist = frame.RightHand, LeftJoints = leftJoints, RightJoints = rightJoints,
                LeftValidMask = frame.LeftHandTracked ? HandJointFrame.AllValid : 0, RightValidMask = frame.RightHandTracked ? HandJointFrame.AllValid : 0 };
            return bound && !stopping;
        }
        public bool TryGetLatestPose(out AvatarPoseFrame value) { value = frame; return bound && !stopping; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRequested()
        {
            if (!Application.isBatchMode || Arg("--lobby-probe") == null) return;
            // --lobby-probe is a switch. Presence is checked separately for the final argument case.
            var item = new GameObject("Explicit synthetic lobby validation");
            DontDestroyOnLoad(item);
            item.AddComponent<LobbyFlowProbe>();
        }
        private void Start()
        {
            try { Initialize(); }
            catch (Exception error) { enabled = false; lease?.Dispose(); lease = null; Debug.LogError("Lobby probe initialization failed: " + error.Message); Application.Quit(2); }
        }
        private void Initialize()
        {
            role = Arg("--lobby-role") ?? "host"; path = Arg("--lobby-report");
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("--lobby-report required");
            lease = LanSmokeProbe.ReserveReportOutput(path);
            double.TryParse(Arg("--lobby-duration"), out duration); if (duration <= 0) duration = 20;
            report = new Report { role = role, syntheticPoses = true };
            started = Time.realtimeSinceStartupAsDouble;
            Application.targetFrameRate = 60;
        }
        private void Update()
        {
            var flow = DesktopRoomFlow.Instance;
            if (!flow) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!clicked)
            {
                report.desktopBeforeVR = flow.IsLobbyVisible && !flow.RoomEntered &&
                    FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length == 0 &&
                    (!XRGeneralSettings.Instance || !XRGeneralSettings.Instance.Manager.activeLoader);
                flow.PortInput.text = "0"; flow.HostButton.onClick.Invoke();
                report.invalidPortRejected = flow.StatusText.Contains("UDP port") && flow.IsLobbyVisible;
                flow.AddressInput.text = Arg("--lobby-address") ?? "127.0.0.1";
                flow.PortInput.text = Arg("--lobby-port") ?? "17893";
                if (role == "host" || role == "reentry") flow.HostButton.onClick.Invoke(); else flow.JoinButton.onClick.Invoke();
                clicked = true;
            }
            if (role == "failure")
            {
                if (now - started < duration) return;
                report.returnedToLobby = flow.IsLobbyVisible && !flow.RoomEntered && !flow.IsBusy;
                report.status = flow.StatusText;
                Finish(); return;
            }
            if (stopping)
            {
                if (flow.IsBusy || SceneManager.GetActiveScene().name != "DesktopLobby") return;
                report.returnedToLobby = flow.IsLobbyVisible && !flow.RoomEntered;
                report.zeroPlayers = FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None).Length == 0;
                report.status = flow.StatusText;
                if (role == "reentry" && !repeated)
                {
                    report.repeatEntryClean = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 0;
                    repeated = true; stopping = false; bound = false; entered = -1;
                    flow.HostButton.onClick.Invoke();
                    return;
                }
                Finish(); return;
            }
            if (!flow.RoomEntered)
            {
                if (now - started > 25) { report.status = flow.StatusText; Finish(); }
                return;
            }
            if (entered < 0) { entered = now; report.roomEntryCount++; }
            report.roomEntered = true;
            var players = FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None);
            report.sawTwoPlayers |= players.Length == 2;
            report.oneRig = FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length == 1;
            double elapsed = now - entered;
            bool near = Arg("--lobby-near-peer") != null;
            float distance = near ? 0 : Mathf.Min(8, (float)elapsed);
            float forward = near ? 2 : 0;
            var rotation = Quaternion.Euler(0, near ? 180 : 30, 0);
            frame = new AvatarPoseFrame { Sequence = ++sequence, LocalCaptureTimeSeconds = now,
                HeadTracked = true, LeftHandTracked = near || elapsed < 5 || elapsed > 6,
                RightHandTracked = near || elapsed < 7 || elapsed > 8,
                Head = new Pose(new Vector3(distance, 1.6f, forward), rotation),
                LeftHand = new Pose(new Vector3(distance - 0.3f, 1.1f, forward + (near ? -0.3f : 0.3f)), rotation),
                RightHand = new Pose(new Vector3(distance + 0.3f, 1.1f, forward + (near ? -0.3f : 0.3f)), rotation) };
            foreach (var player in players)
            {
                if (player.IsOwner && !bound)
                {
                    player.BindLocalSource(this); player.GetComponent<NetworkHandJointSync>()?.BindLocalSource(this); bound = true;
                    var local = GameObject.Find("Local AvatarRoot").transform;
                    local.GetComponent<HandSkeletonView>()?.BindSource(this);
                    local.GetComponent<AvatarPoseView>().Configure(this, local, local.Find("Local Head Avatar"),
                        local.Find("Local Left Hand"), local.Find("Local Right Hand"), true);
                }
                if (!player.IsOwner)
                {
                    var jointSync = player.GetComponent<NetworkHandJointSync>();
                    if (jointSync)
                    {
                        report.receivedJointPackets = Math.Max(report.receivedJointPackets, jointSync.ReceivedCount);
                        if (jointSync.TryGetLatestHandJoints(out var jointFrame) && HandJointFrame.IsValid(jointFrame.RightValidMask, 10))
                        {
                            float y = jointFrame.RightJoints[10].position.y;
                            if (!fingerSet) { firstFinger = y; fingerSet = true; }
                            report.remoteFingerArticulation |= Mathf.Abs(y - firstFinger) > 0.01f;
                        }
                    }
                    report.received = Math.Max(report.received, player.ReceivedCount);
                    var visuals = player.transform.Find("Remote pose visuals");
                    if (visuals)
                    {
                        var jointView = visuals.GetComponent<HandSkeletonView>();
                        if (jointView)
                        {
                            report.remoteJointSkeleton |= jointView.LeftValidCount == 26 && jointView.RightValidCount == 26;
                            report.independentJointLoss |= (jointView.LeftValidCount == 0) != (jointView.RightValidCount == 0);
                            report.jointRecovery |= report.independentJointLoss && jointView.LeftValidCount == 26 && jointView.RightValidCount == 26;
                        }
                        var left = visuals.Find("Remote left wrist"); var right = visuals.Find("Remote right wrist");
                        var head = visuals.Find("Remote head");
                        report.remoteHandsAndHeadVisible |= left.gameObject.activeInHierarchy && right.gameObject.activeInHierarchy && head.gameObject.activeInHierarchy;
                        report.sawIndependentLoss |= left.gameObject.activeInHierarchy != right.gameObject.activeInHierarchy;
                        report.sawRecovery |= report.sawIndependentLoss && left.gameObject.activeInHierarchy && right.gameObject.activeInHierarchy;
                    }
                    if (player.TryGetLatestPose(out var remote)) report.receivedBeyondFiveMeters |= remote.HeadTracked && remote.Head.position.x > 5;
                }
            }
            var localRoot = GameObject.Find("Local AvatarRoot");
            if (localRoot && localRoot.GetComponent<HandSkeletonView>())
                report.localJointSkeleton |= localRoot.GetComponent<HandSkeletonView>().LeftValidCount == 26 && localRoot.GetComponent<HandSkeletonView>().RightValidCount == 26;
            if (localRoot) report.localHandsVisible |= localRoot.transform.Find("Local Left Hand").gameObject.activeInHierarchy &&
                localRoot.transform.Find("Local Right Hand").gameObject.activeInHierarchy;
            if (elapsed >= duration) { stopping = true; flow.Disconnect(); }
        }
        private void Finish()
        {
            report.xrStopped = !XRGeneralSettings.Instance || !XRGeneralSettings.Instance.Manager.activeLoader;
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            lease?.Dispose(); lease = null;
            enabled = false; Application.Quit();
        }
        private void OnDestroy() => lease?.Dispose();
        private static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 ? index + 1 < args.Length ? args[index + 1] : "" : null;
        }
    }
}
