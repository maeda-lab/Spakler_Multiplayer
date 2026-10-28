using System;
using System.IO;
using System.Text;
using QuestLAN.Networking;
using QuestLAN.XR;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace QuestLAN.Integration
{
    /// <summary>Opt-in real-device observation only. Does not provide or modify tracking/input.</summary>
    public sealed class LobbyLinkCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool desktopLobbyObserved, xrStartedBeforeConnection, enteredRoom, displayRunning;
            public bool peerIsSynthetic, returnedToLobby, completed, interrupted;
            public int samples, headTrackedFrames, leftTrackedFrames, rightTrackedFrames, movementSamples;
            public int localLeftVisibleFrames, localRightVisibleFrames, remoteHeadVisibleFrames, remoteHandsVisibleFrames;
            public float maximumRigTravel, maximumHeadYawChange, maximumCameraTravel, maximumRemoteHeadTravel;
            public double roomSeconds;
            public string status, startedUtc, simultaneousTracking;
            public int leftJointFrames, rightJointFrames, maximumLeftJoints, maximumRightJoints, remoteJointFrames;
        }
        private Report report;
        private FileStream output;
        private string path;
        private bool syntheticPeer, yawSet, leaving;
        private float yaw;
        private Vector3 origin, cameraOrigin, remoteOrigin;
        private bool remoteOriginSet;
        private double entered = -1, nextWrite;
        public void Configure(string outputPath, bool mockPeer) { path = outputPath; syntheticPeer = mockPeer; }
        private void Start()
        {
            try
            {
                DontDestroyOnLoad(gameObject);
                report = new Report { peerIsSynthetic = syntheticPeer, startedUtc = DateTime.UtcNow.ToString("O") };
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                Write();
            }
            catch (Exception error) { Debug.LogError("Link check report could not start: " + error.Message); enabled = false; }
        }
        private void Update()
        {
            var flow = DesktopRoomFlow.Instance;
            if (!flow || output == null) return;
            var network = FindFirstObjectByType<NetworkManager>();
            var xr = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
            var display = xr && xr.activeLoader ? xr.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
            if (!report.enteredRoom) report.xrStartedBeforeConnection |= display != null && display.running && (!network || !network.IsConnectedClient);
            if (!flow.RoomEntered)
            {
                if (entered < 0) report.desktopLobbyObserved |= flow.IsLobbyVisible;
                if (entered >= 0 && !flow.IsBusy)
                {
                    report.returnedToLobby = flow.IsLobbyVisible; report.completed = true; report.status = flow.StatusText;
                    Write(); output.Dispose(); output = null; enabled = false;
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                }
                return;
            }
            var rig = FindFirstObjectByType<XROrigin>();
            if (!rig) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (entered < 0) { entered = now; origin = rig.transform.position; cameraOrigin = rig.Camera.transform.position; }
            report.enteredRoom = true; report.samples++; report.roomSeconds = now - entered;
            report.displayRunning |= display != null && display.running;
            var source = rig.GetComponent<XRHandsPoseSource>();
            if (source.TryGetLatestPose(out var pose))
            {
                if (pose.HeadTracked)
                {
                    report.headTrackedFrames++;
                    if (!yawSet) { yawSet = true; yaw = rig.Camera.transform.eulerAngles.y; }
                    report.maximumHeadYawChange = Mathf.Max(report.maximumHeadYawChange, Mathf.Abs(Mathf.DeltaAngle(yaw, rig.Camera.transform.eulerAngles.y)));
                }
                if (pose.LeftHandTracked) report.leftTrackedFrames++;
                if (pose.RightHandTracked) report.rightTrackedFrames++;
            }
            if (source.TryGetLatestHandJoints(out var hands))
            {
                int leftCount = Count(hands.LeftValidMask), rightCount = Count(hands.RightValidMask);
                if (leftCount > 0) report.leftJointFrames++;
                if (rightCount > 0) report.rightJointFrames++;
                report.maximumLeftJoints = Mathf.Max(report.maximumLeftJoints, leftCount);
                report.maximumRightJoints = Mathf.Max(report.maximumRightJoints, rightCount);
                report.simultaneousTracking = SimultaneousHandsFeature.Status;
            }
            report.maximumRigTravel = Mathf.Max(report.maximumRigTravel, Vector3.Distance(origin, rig.transform.position));
            report.maximumCameraTravel = Mathf.Max(report.maximumCameraTravel, Vector3.Distance(cameraOrigin, rig.Camera.transform.position));
            report.movementSamples = rig.GetComponent<HeadRelativeLocomotion>().MovementSamples;
            var local = GameObject.Find("Local AvatarRoot").transform;
            if (local.Find("Local Left Hand").gameObject.activeInHierarchy) report.localLeftVisibleFrames++;
            if (local.Find("Local Right Hand").gameObject.activeInHierarchy) report.localRightVisibleFrames++;
            foreach (var player in FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None))
            {
                if (player.IsOwner) continue;
                var visuals = player.transform.Find("Remote pose visuals");
                if (!visuals) continue;
                var jointView = visuals.GetComponent<QuestLAN.Avatar.HandSkeletonView>();
                if (jointView && jointView.LeftValidCount > 0 && jointView.RightValidCount > 0) report.remoteJointFrames++;
                var remoteHead = visuals.Find("Remote head");
                if (remoteHead.gameObject.activeInHierarchy)
                {
                    report.remoteHeadVisibleFrames++;
                    if (!remoteOriginSet) { remoteOriginSet = true; remoteOrigin = remoteHead.position; }
                    report.maximumRemoteHeadTravel = Mathf.Max(report.maximumRemoteHeadTravel, Vector3.Distance(remoteOrigin, remoteHead.position));
                }
                if (visuals.Find("Remote left wrist").gameObject.activeInHierarchy && visuals.Find("Remote right wrist").gameObject.activeInHierarchy) report.remoteHandsVisibleFrames++;
            }
            if (now >= nextWrite) { Write(); nextWrite = now + 5; }
            if (report.roomSeconds >= 90 && !leaving) { leaving = true; flow.Disconnect(); }
        }
        private static int Count(uint mask) { int result = 0; while (mask != 0) { result += (int)(mask & 1); mask >>= 1; } return result; }
        private void Write()
        {
            if (output == null) return;
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true));
            output.Position = 0; output.SetLength(data.Length); output.Write(data, 0, data.Length); output.Flush();
        }
        private void OnDestroy()
        {
            if (output == null) return;
            report.interrupted = !report.completed; Write(); output.Dispose(); output = null;
        }
    }
}
