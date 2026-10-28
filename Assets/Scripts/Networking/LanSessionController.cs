using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using QuestLAN.Shared;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace QuestLAN.Networking
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class LanSessionController : MonoBehaviour
    {
        public const ushort SessionProtocolVersion = 4;
        [SerializeField] private MonoBehaviour localPoseSource;
        [SerializeField] private bool diagnosticLogs = true;
        [SerializeField] private string hostAddress = "127.0.0.1";
        [SerializeField] private string portText = "7777";
        private NetworkManager manager;
        private UnityTransport transport;
        private readonly Dictionary<ulong, int> slots = new Dictionary<ulong, int>();
        private bool requested;
        private bool stopping;
        private bool terminalFailure;
        private double connectingSince;
        private bool showStatus = true;
        [SerializeField] private bool desktopControlsVisible = true;
        public bool DesktopControlsVisible { get => desktopControlsVisible; set => desktopControlsVisible = value; }
        public string HostAddress { get => hostAddress; set => hostAddress = value; }
        public string PortText { get => portText; set => portText = value; }
        public bool CanStart => manager && !requested && !stopping && !manager.IsListening && !manager.ShutdownInProgress;
        public bool CanDisconnect => manager && !stopping && (requested || manager.IsListening);
        public int PlayerCount
        {
            get
            {
                if (!manager || !manager.IsListening || manager.SpawnManager == null) return 0;
                int count = 0;
                foreach (var item in manager.SpawnManager.SpawnedObjectsList)
                    if (item && item.IsSpawned && item.IsPlayerObject) count++;
                return count;
            }
        }
        public string Role => manager && manager.IsHost ? "Host" : manager && manager.IsClient ? "Client" : "Offline";
        public string Status { get; private set; } = "Idle";
        public MonoBehaviour LocalPoseSource => localPoseSource;
        public bool DiagnosticLogs => diagnosticLogs;

        public void Configure(MonoBehaviour source) => localPoseSource = source;
        private void Awake() { manager = GetComponent<NetworkManager>(); transport = GetComponent<UnityTransport>(); }
        private void OnEnable()
        {
            if (!manager) Awake();
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
            manager.OnServerStarted += ServerStarted;
            manager.OnServerStopped += Stopped;
            manager.OnClientStopped += Stopped;
            manager.ConnectionApprovalCallback = Approve;
        }

        private void OnDisable()
        {
            if (!manager) return;
            Disconnect();
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
            manager.OnTransportFailure -= TransportFailed;
            manager.OnServerStarted -= ServerStarted;
            manager.OnServerStopped -= Stopped;
            manager.OnClientStopped -= Stopped;
            manager.ConnectionApprovalCallback = null;
        }

        public bool StartHost(string address = "127.0.0.1", ushort port = 7777) => StartSession(true, address, port);
        public bool StartClient(string address, ushort port = 7777) => StartSession(false, address, port);

        private bool StartSession(bool host, string address, ushort port)
        {
            if (requested || manager.IsListening || manager.ShutdownInProgress) return false;
            if (port == 0 || !IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            { SetStatus("Failed: enter a valid IPv4 and UDP port 1–65535"); return false; }
            slots.Clear();
            stopping = false;
            terminalFailure = false;
            requested = true;
            connectingSince = Time.realtimeSinceStartupAsDouble;
            transport.SetConnectionData(address, port, host ? "0.0.0.0" : null);
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ProtocolVersion = SessionProtocolVersion;
            manager.NetworkConfig.ConnectionData = new[] { NetworkAvatarPosePacket.ProtocolVersion };
            manager.NetworkConfig.EnableSceneManagement = false;
            SetStatus(host ? "Starting Host" : "Connecting");
            try
            {
                bool result = host ? manager.StartHost() : manager.StartClient();
                if (!result) { requested = false; terminalFailure = true; SetStatus("Failed: transport could not start; check address/port and logs"); }
                return result;
            }
            catch (System.Exception exception)
            {
                requested = false;
                terminalFailure = true;
                manager.Shutdown();
                SetStatus("Failed: " + exception.Message);
                return false;
            }
        }

        public void Disconnect()
        {
            if (!requested && !manager.IsListening) return;
            stopping = true;
            SetStatus("Stopping");
            manager.Shutdown();
        }

        private void Update()
        {
            if (stopping && !manager.IsListening && !manager.ShutdownInProgress) CompleteStop();
            if (requested && !stopping && !manager.IsServer && !manager.IsConnectedClient &&
                Time.realtimeSinceStartupAsDouble - connectingSince > 10)
            {
                terminalFailure = true;
                SetStatus("Failed: connection timed out; check Host, IPv4, LAN, UDP port and AP isolation");
                manager.Shutdown();
                requested = false;
            }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool protocol = request.Payload != null && request.Payload.Length == 1 &&
                request.Payload[0] == NetworkAvatarPosePacket.ProtocolVersion;
            bool full = slots.Count >= 2;
            response.Approved = protocol && !full;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            if (!response.Approved) { response.Reason = full ? "Room full: maximum two players including Host" : "Incompatible pose protocol"; return; }
            int slot = slots.ContainsValue(0) ? 1 : 0;
            slots[request.ClientNetworkId] = slot;
            response.Position = new Vector3(slot == 0 ? -0.65f : 0.65f, 0, 0);
            response.Rotation = Quaternion.identity;
        }

        private void Connected(ulong id)
        {
            if (id == manager.LocalClientId) SetStatus(manager.IsHost ? "Host" : "Connected");
            Log($"Client connected: {id}");
        }
        private void Disconnected(ulong id)
        {
            slots.Remove(id);
            Log($"Client disconnected: {id}");
            if (id != manager.LocalClientId || manager.IsServer) return;
            if (!stopping)
            {
                terminalFailure = true;
                SetStatus("Disconnected: " + (string.IsNullOrEmpty(manager.DisconnectReason) ? "Host stopped or connection lost" : manager.DisconnectReason));
            }
            requested = false;
        }
        private void ServerStarted() => SetStatus("Host");
        private void TransportFailed() { terminalFailure = true; requested = false; SetStatus("Failed: transport error; see Console/player log"); }
        private void Stopped(bool _) => CompleteStop();
        private void CompleteStop()
        {
            slots.Clear(); requested = false; stopping = false;
            if (!terminalFailure) SetStatus("Idle");
        }
        private void SetStatus(string value) { if (Status == value) return; Status = value; Log(value); }
        public void Log(string message) { if (diagnosticLogs) Debug.Log("[LAN] " + message, this); }

        private void OnGUI()
        {
            if (!desktopControlsVisible) return;
            GUILayout.BeginArea(new Rect(12, 12, 400, 410), GUI.skin.box);
            GUILayout.Label("QuestLAN — two-player LAN");
            GUILayout.Label("Status: " + Status);
            bool idle = CanStart;
            GUI.enabled = idle;
            GUILayout.Label("Host IP (IPv4)");
            hostAddress = GUILayout.TextField(hostAddress);
            GUILayout.Label("UDP port"); portText = GUILayout.TextField(portText);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Host")) StartFromUI(true);
            if (GUILayout.Button("Join")) StartFromUI(false);
            GUILayout.EndHorizontal();
            GUI.enabled = CanDisconnect;
            if (GUILayout.Button("Disconnect")) Disconnect();
            GUI.enabled = true;
            if (GUILayout.Button(showStatus ? "Hide Status" : "Show Status")) showStatus = !showStatus;
            if (showStatus)
            {
                GUILayout.Label($"Role: {Role} | Players: {PlayerCount}/2");
                GUILayout.Label(manager.IsHost ? "Disconnect closes the room for everyone." : "Disconnect leaves the room; the application stays open.");
            }
            diagnosticLogs = GUILayout.Toggle(diagnosticLogs, "Diagnostic logs");
            foreach (var player in FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned) continue;
                GUILayout.Label($"Player {player.OwnerClientId}: {(player.IsOwner ? "Local Owner" : "Remote")} | received {player.ReceivedCount}");
                var signals = player.GetComponent<NetworkPlayerSignals>();
                if (!signals) continue;
                GUILayout.Label($"Pose sent {signals.SendHz:F1} Hz / received {signals.ReceiveHz:F1} Hz | color {signals.ColorIndex.Value}");
                if (!player.IsOwner) continue;
                var rtt = signals.Rtt;
                GUILayout.Label(IsHostLabel(manager) ? "Host: local server (no remote RTT)" :
                    rtt.TimedOut ? "RTT: timeout" : rtt.HasSample ? $"RTT {rtt.CurrentMilliseconds:F1} ms / smoothed {rtt.SmoothedMilliseconds:F1} ms" : "RTT: waiting");
                if (GUILayout.Button("Change synchronized color")) signals.RequestColorChange();
            }
            GUILayout.EndArea();
        }

        private static bool IsHostLabel(NetworkManager value) => value.IsHost;

        private void StartFromUI(bool host)
        {
            if (!ushort.TryParse(portText, out var port) || port == 0) { SetStatus("Failed: UDP port must be 1–65535"); return; }
            StartSession(host, host ? "127.0.0.1" : hostAddress, port);
        }
    }
}
