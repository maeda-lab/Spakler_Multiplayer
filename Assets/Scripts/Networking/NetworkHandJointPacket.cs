using QuestLAN.Shared;
using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Networking
{
    /// <summary>At most 805 bytes: wrists float, wrist-relative joints millimetres/int16 quaternion.</summary>
    public struct NetworkHandJointPacket : INetworkSerializable
    {
        public byte Version;
        public HandJointFrame Frame;
        public const byte ProtocolVersion = 1;
        public const int MaximumPayloadBytes = 805;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Version); s.SerializeValue(ref Frame.Sequence); s.SerializeValue(ref Frame.LocalCaptureTimeSeconds);
            s.SerializeValue(ref Frame.LeftValidMask); s.SerializeValue(ref Frame.RightValidMask);
            Wrist(s, ref Frame.LeftWrist); Wrist(s, ref Frame.RightWrist);
            Joints(s, Frame.LeftValidMask, ref Frame.LeftJoints); Joints(s, Frame.RightValidMask, ref Frame.RightJoints);
        }
        private static void Wrist<T>(BufferSerializer<T> s, ref Pose p) where T : IReaderWriter
        { s.SerializeValue(ref p.position); s.SerializeValue(ref p.rotation); }
        private static void Joints<T>(BufferSerializer<T> s, uint mask, ref Pose[] poses) where T : IReaderWriter
        {
            if (s.IsReader) poses = new Pose[26];
            for (int i = 0; i < 26; i++)
            {
                if (!HandJointFrame.IsValid(mask, i)) continue;
                Pose p = s.IsReader ? Pose.identity : poses[i];
                short x = Pack(p.position.x, 1000), y = Pack(p.position.y, 1000), z = Pack(p.position.z, 1000);
                var q = p.rotation.normalized;
                short qx = Pack(q.x, 32767), qy = Pack(q.y, 32767), qz = Pack(q.z, 32767), qw = Pack(q.w, 32767);
                s.SerializeValue(ref x); s.SerializeValue(ref y); s.SerializeValue(ref z);
                s.SerializeValue(ref qx); s.SerializeValue(ref qy); s.SerializeValue(ref qz); s.SerializeValue(ref qw);
                if (s.IsReader) poses[i] = new Pose(new Vector3(x, y, z) / 1000f, new Quaternion(qx / 32767f, qy / 32767f, qz / 32767f, qw / 32767f));
            }
        }
        private static short Pack(float value, float scale) => (short)Mathf.Clamp(Mathf.RoundToInt(value * scale), -32767, 32767);
        public bool TryValidate()
        {
            return Version == ProtocolVersion && Frame.LocalCaptureTimeSeconds >= 0 && !double.IsInfinity(Frame.LocalCaptureTimeSeconds) &&
                ValidHand(Frame.LeftValidMask, Frame.LeftWrist, Frame.LeftJoints) && ValidHand(Frame.RightValidMask, Frame.RightWrist, Frame.RightJoints);
        }
        private static bool ValidHand(uint mask, Pose wrist, Pose[] joints)
        {
            if ((mask & ~HandJointFrame.AllValid) != 0) return false;
            if (mask == 0) return true;
            if (!HandJointFrame.IsValid(mask, 1) || joints == null || joints.Length != 26 || !ValidPose(wrist, 50)) return false;
            for (int i = 0; i < 26; i++) if (HandJointFrame.IsValid(mask, i) && !ValidPose(joints[i], 1)) return false;
            return true;
        }
        private static bool ValidPose(Pose p, float distance)
        {
            float length = Quaternion.Dot(p.rotation, p.rotation);
            return p.position.sqrMagnitude <= distance * distance && length >= 0.5f && length <= 1.5f &&
                !float.IsNaN(p.position.sqrMagnitude) && !float.IsNaN(length);
        }
    }
}
