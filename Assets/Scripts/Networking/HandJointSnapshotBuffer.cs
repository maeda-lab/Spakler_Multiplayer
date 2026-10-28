using QuestLAN.Shared;
using UnityEngine;

namespace QuestLAN.Networking
{
    public sealed class HandJointSnapshotBuffer
    {
        private HandJointFrame previous, latest;
        private double previousArrival, arrival;
        private bool received;
        private readonly Pose[] left = new Pose[26], right = new Pose[26];
        public int AcceptedCount { get; private set; }
        public void Reset() { received = false; AcceptedCount = 0; previous = latest = default; }
        public bool Push(HandJointFrame frame, double now)
        {
            if (received && unchecked((int)(frame.Sequence - latest.Sequence)) <= 0) return false;
            previous = latest; previousArrival = arrival;
            latest = frame.Copy(); arrival = now; received = true; AcceptedCount++; return true;
        }
        public bool TrySample(double now, double delay, double timeout, out HandJointFrame frame)
        {
            frame = latest;
            if (!received) return false;
            if (now - arrival > timeout) { frame.LeftValidMask = frame.RightValidMask = 0; return true; }
            float t = arrival > previousArrival ? Mathf.Clamp01((float)((now - delay - previousArrival) / (arrival - previousArrival))) : 1;
            frame.LeftWrist = Blend(previous.LeftWrist, latest.LeftWrist, previous.LeftValidMask != 0, t);
            frame.RightWrist = Blend(previous.RightWrist, latest.RightWrist, previous.RightValidMask != 0, t);
            BlendJoints(previous.LeftJoints, latest.LeftJoints, previous.LeftValidMask & latest.LeftValidMask, left, t);
            BlendJoints(previous.RightJoints, latest.RightJoints, previous.RightValidMask & latest.RightValidMask, right, t);
            frame.LeftJoints = left; frame.RightJoints = right;
            return true;
        }
        private static Pose Blend(Pose a, Pose b, bool valid, float t) => valid ?
            new Pose(Vector3.Lerp(a.position, b.position, t), Quaternion.Slerp(a.rotation, b.rotation, t)) : b;
        private static void BlendJoints(Pose[] a, Pose[] b, uint common, Pose[] output, float t)
        {
            for (int i = 0; i < 26; i++) output[i] = b == null ? Pose.identity : Blend(a == null ? Pose.identity : a[i], b[i], HandJointFrame.IsValid(common, i), t);
        }
    }
}
