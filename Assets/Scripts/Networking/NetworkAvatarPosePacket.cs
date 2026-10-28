using QuestLAN.Shared;
using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Networking
{
    public struct NetworkAvatarPosePacket : INetworkSerializable
    {
        public const byte ProtocolVersion = 1;
        public byte Version;
        public AvatarPoseFrame Frame;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Version);
            serializer.SerializeValue(ref Frame.Sequence);
            serializer.SerializeValue(ref Frame.LocalCaptureTimeSeconds);
            serializer.SerializeValue(ref Frame.HeadTracked);
            serializer.SerializeValue(ref Frame.LeftHandTracked);
            serializer.SerializeValue(ref Frame.RightHandTracked);
            SerializePose(serializer, ref Frame.Head);
            SerializePose(serializer, ref Frame.LeftHand);
            SerializePose(serializer, ref Frame.RightHand);
        }

        private static void SerializePose<T>(BufferSerializer<T> serializer, ref Pose pose) where T : IReaderWriter
        {
            serializer.SerializeValue(ref pose.position);
            serializer.SerializeValue(ref pose.rotation);
        }

        public bool TryValidate(float maximumDistance = 50f)
        {
            if (Version != ProtocolVersion || double.IsNaN(Frame.LocalCaptureTimeSeconds) ||
                double.IsInfinity(Frame.LocalCaptureTimeSeconds) || Frame.LocalCaptureTimeSeconds < 0) return false;
            return ValidatePose(ref Frame.Head, Frame.HeadTracked, maximumDistance) &&
                ValidatePose(ref Frame.LeftHand, Frame.LeftHandTracked, maximumDistance) &&
                ValidatePose(ref Frame.RightHand, Frame.RightHandTracked, maximumDistance);
        }

        private static bool ValidatePose(ref Pose pose, bool tracked, float maximumDistance)
        {
            if (!tracked) { pose = Pose.identity; return true; }
            var p = pose.position;
            var q = pose.rotation;
            if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(q.x) ||
                !Finite(q.y) || !Finite(q.z) || !Finite(q.w) || p.sqrMagnitude > maximumDistance * maximumDistance) return false;
            float length = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (!Finite(length) || length < 0.0001f || length > 100f) return false;
            float inverse = 1f / Mathf.Sqrt(length);
            pose.rotation = new Quaternion(q.x * inverse, q.y * inverse, q.z * inverse, q.w * inverse);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
