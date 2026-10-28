using QuestLAN.Shared;
using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkHandJointSync : NetworkBehaviour, IHandJointSource
    {
        private MonoBehaviour source;
        private readonly HandJointSnapshotBuffer buffer = new HandJointSnapshotBuffer();
        private uint sentSequence, sourceSequence;
        private bool paused, sampled;
        private double nextSend, lastFresh;
        public int ReceivedCount => buffer.AcceptedCount;
        public void BindLocalSource(MonoBehaviour value)
        {
            if (IsSpawned && !IsOwner) throw new System.InvalidOperationException("Only owner may bind joints");
            source = value; sampled = false; lastFresh = 0;
        }
        public override void OnNetworkSpawn()
        {
            buffer.Reset(); paused = sampled = false; sentSequence = 0; nextSend = 0;
            if (IsOwner) BindLocalSource(NetworkManager.GetComponent<LanSessionController>().LocalPoseSource);
        }
        public override void OnNetworkDespawn() { source = null; sampled = false; buffer.Reset(); }
        private void Update()
        {
            if (!IsSpawned || !IsOwner || paused) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSend) return;
            nextSend = now + 0.05;
            HandJointFrame frame = default;
            bool available = source && source is IHandJointSource jointSource && jointSource.TryGetLatestHandJoints(out frame);
            if (available && (!sampled || frame.Sequence != sourceSequence))
            { sampled = true; sourceSequence = frame.Sequence; lastFresh = now; }
            if (!available || now - lastFresh > 0.2) frame.LeftValidMask = frame.RightValidMask = 0;
            frame.Sequence = ++sentSequence;
            if (!available) frame.LocalCaptureTimeSeconds = now;
            var packet = new NetworkHandJointPacket { Version = 1, Frame = frame };
            if (packet.TryValidate()) SubmitJointsRpc(packet);
        }
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitJointsRpc(NetworkHandJointPacket packet, RpcParams args = default)
        {
            if (!IsServer || args.Receive.SenderClientId != OwnerClientId || !packet.TryValidate() || !Accept(packet.Frame)) return;
            RelayJointsRpc(packet);
        }
        [Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        private void RelayJointsRpc(NetworkHandJointPacket packet, RpcParams args = default)
        {
            if (args.Receive.SenderClientId == Unity.Netcode.NetworkManager.ServerClientId && packet.TryValidate()) Accept(packet.Frame);
        }
        private bool Accept(HandJointFrame frame)
        {
            return buffer.Push(frame, Time.realtimeSinceStartupAsDouble);
        }

        public bool TryGetLatestHandJoints(out HandJointFrame frame)
        {
            frame = default;
            if (!IsSpawned || paused) return false;
            if (IsOwner) return source && source is IHandJointSource jointSource && jointSource.TryGetLatestHandJoints(out frame);
            return buffer.TrySample(Time.realtimeSinceStartupAsDouble, 0.1, 0.5, out frame);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value; buffer.Reset(); sampled = false;
            if (value && IsSpawned && IsOwner)
                SubmitJointsRpc(new NetworkHandJointPacket { Version = 1, Frame = new HandJointFrame { Sequence = ++sentSequence, LocalCaptureTimeSeconds = Time.realtimeSinceStartupAsDouble } });
        }
    }
}
