using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Networking
{
    /// <summary>Owner ping/pong and server-authoritative replicated test color.</summary>
    [RequireComponent(typeof(NetworkPoseSync))]
    public sealed class NetworkPlayerSignals : NetworkBehaviour
    {
        public readonly NetworkVariable<byte> ColorIndex = new NetworkVariable<byte>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public RttTracker Rtt { get; } = new RttTracker();
        public int ColorRequestsSent { get; private set; }
        public int ServerColorChanges { get; private set; }
        public float SendHz { get; private set; }
        public float ReceiveHz { get; private set; }
        private double nextPing, nextRate, lastRate, lastColor = double.NegativeInfinity;
        private int lastSent, lastReceived;
        private bool paused;
        private NetworkPoseSync pose;

        public override void OnNetworkSpawn()
        {
            pose = GetComponent<NetworkPoseSync>();
            Rtt.Reset(); paused = false;
            nextPing = lastRate = Time.realtimeSinceStartupAsDouble;
            nextRate = lastRate + 1;
            lastSent = lastReceived = 0;
            SendHz = ReceiveHz = 0;
            lastColor = double.NegativeInfinity;
            ColorRequestsSent = ServerColorChanges = 0;
            ColorIndex.OnValueChanged += ColorChanged;
        }

        public override void OnNetworkDespawn() { ColorIndex.OnValueChanged -= ColorChanged; Rtt.Reset(); SendHz = ReceiveHz = 0; }

        private void ColorChanged(byte previous, byte current) => NetworkManager.GetComponent<LanSessionController>()?
            .Log($"Player {OwnerClientId} color state {previous} -> {current}");

        private void Update()
        {
            if (!IsSpawned || paused) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (IsOwner)
            {
                Rtt.Tick(now);
                if (now >= nextPing) { nextPing = now + 1.5; PingRpc(Rtt.Begin(now)); }
            }
            if (now < nextRate) return;
            double elapsed = now - lastRate;
            SendHz = (float)System.Math.Max(0, (pose.SentCount - lastSent) / elapsed);
            ReceiveHz = (float)System.Math.Max(0, (pose.ReceivedCount - lastReceived) / elapsed);
            lastSent = pose.SentCount; lastReceived = pose.ReceivedCount;
            lastRate = now; nextRate = now + 1;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        private void PingRpc(uint token, RpcParams parameters = default)
        {
            if (IsServer && parameters.Receive.SenderClientId == OwnerClientId) PongRpc(token);
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        private void PongRpc(uint token, RpcParams parameters = default)
        {
            if (IsOwner && parameters.Receive.SenderClientId == Unity.Netcode.NetworkManager.ServerClientId)
                Rtt.Complete(token, Time.realtimeSinceStartupAsDouble);
        }

        public void RequestColorChange()
        {
            if (!IsSpawned || !IsOwner || paused) return;
            ColorRequestsSent++;
            ChangeColorRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ChangeColorRpc(RpcParams parameters = default)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!IsServer || parameters.Receive.SenderClientId != OwnerClientId || now - lastColor < 0.25) return;
            lastColor = now;
            ServerColorChanges++;
            ColorIndex.Value = (byte)((ColorIndex.Value + 1) % 4);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value; Rtt.Reset();
            nextPing = Time.realtimeSinceStartupAsDouble;
        }
    }
}
