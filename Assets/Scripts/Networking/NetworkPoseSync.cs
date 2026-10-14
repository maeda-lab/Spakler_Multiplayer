using QuestLAN.Shared;
using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPoseSync : NetworkBehaviour, IAvatarPoseSource
    {
        [SerializeField, Range(1, 60)] private float sendRate = 20;
        [SerializeField, Range(0, 0.25f)] private float interpolationDelay = 0.1f;
        [SerializeField, Range(0.1f, 2)] private float receiveTimeout = 0.5f;
        private MonoBehaviour localSource;
        private LanSessionController session;
        private readonly PoseSnapshotBuffer receiveBuffer = new PoseSnapshotBuffer();
        private uint transportSequence;
        private uint sourceSequence;
        private bool hasSourceSequence;
        private double nextSend;
        private double lastSent;
        private bool paused;
        private int lastTrackingMask = -1;
        public int ReceivedCount => receiveBuffer.AcceptedCount;
        public uint ReceivedSequence => receiveBuffer.LastSequence;
        public int SentCount { get; private set; }

        public void BindLocalSource(MonoBehaviour source)
        {
            if (IsSpawned && !IsOwner) throw new System.InvalidOperationException("Only local owner may bind a pose source");
            localSource = source;
            hasSourceSequence = false;
        }

        public override void OnNetworkSpawn()
        {
            receiveBuffer.Reset(); transportSequence = 0; SentCount = 0;
            hasSourceSequence = false; nextSend = 0; lastSent = 0; paused = false;
            session = NetworkManager.GetComponent<LanSessionController>();
            if (IsOwner && session) BindLocalSource(session.LocalPoseSource);
            session?.Log($"Player spawned: owner={OwnerClientId}, local={IsOwner}");
        }
        public override void OnNetworkDespawn()
        {
            session?.Log($"Player despawned: owner={OwnerClientId}");
            receiveBuffer.Reset(); localSource = null; hasSourceSequence = false;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || paused) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSend) return;
            nextSend = now + 1d / Mathf.Max(1, sendRate);
            bool available = localSource && localSource is IAvatarPoseSource source && source.TryGetLatestPose(out _);
            AvatarPoseFrame frame = default;
            if (available) ((IAvatarPoseSource)localSource).TryGetLatestPose(out frame);
            bool fresh = available && (!hasSourceSequence || frame.Sequence != sourceSequence);
            if (!fresh && now - lastSent < 0.2) return;
            // A source that stopped sampling cannot keep claiming tracking on heartbeat packets.
            if (!fresh) frame.HeadTracked = frame.LeftHandTracked = frame.RightHandTracked = false;
            if (fresh) { sourceSequence = frame.Sequence; hasSourceSequence = true; }
            frame.Sequence = unchecked(++transportSequence);
            if (!available) frame.LocalCaptureTimeSeconds = now;
            var packet = new NetworkAvatarPosePacket { Version = NetworkAvatarPosePacket.ProtocolVersion, Frame = frame };
            if (!packet.TryValidate()) return;
            SubmitPoseRpc(packet);
            lastSent = now; SentCount++;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitPoseRpc(NetworkAvatarPosePacket packet, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId || !packet.TryValidate()) return;
            if (!receiveBuffer.Push(packet.Frame, Time.realtimeSinceStartupAsDouble)) return;
            int mask = (packet.Frame.HeadTracked ? 1 : 0) | (packet.Frame.LeftHandTracked ? 2 : 0) | (packet.Frame.RightHandTracked ? 4 : 0);
            if (mask != lastTrackingMask) { session?.Log($"Player {OwnerClientId} tracking mask: {mask}"); lastTrackingMask = mask; }
            RelayPoseRpc(packet);
        }

        [Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        private void RelayPoseRpc(NetworkAvatarPosePacket packet, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId || !packet.TryValidate()) return;
            receiveBuffer.Push(packet.Frame, Time.realtimeSinceStartupAsDouble);
        }

        public bool TryGetLatestPose(out AvatarPoseFrame frame)
        {
            frame = default;
            if (!IsSpawned || paused) return false;
            if (IsOwner)
                return localSource && localSource is IAvatarPoseSource source && source.TryGetLatestPose(out frame);
            return receiveBuffer.TrySample(Time.realtimeSinceStartupAsDouble, interpolationDelay, receiveTimeout, out frame);
        }

        private void OnApplicationPause(bool pause)
        {
            paused = pause;
            receiveBuffer.Reset();
            hasSourceSequence = false;
            if (pause && IsSpawned && IsOwner)
            {
                SubmitPoseRpc(new NetworkAvatarPosePacket
                {
                    Version = NetworkAvatarPosePacket.ProtocolVersion,
                    Frame = new AvatarPoseFrame { Sequence = unchecked(++transportSequence), LocalCaptureTimeSeconds = Time.realtimeSinceStartupAsDouble }
                });
            }
        }
    }
}
