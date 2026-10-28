using UnityEngine;

namespace QuestLAN.Shared
{
    // Stable shared order; the XR adapter owns the mapping to vendor/provider enums.
    public enum HandJointIndex
    {
        Palm, Wrist, ThumbMetacarpal, ThumbProximal, ThumbDistal, ThumbTip,
        IndexMetacarpal, IndexProximal, IndexIntermediate, IndexDistal, IndexTip,
        MiddleMetacarpal, MiddleProximal, MiddleIntermediate, MiddleDistal, MiddleTip,
        RingMetacarpal, RingProximal, RingIntermediate, RingDistal, RingTip,
        LittleMetacarpal, LittleProximal, LittleIntermediate, LittleDistal, LittleTip
    }
    public struct HandJointFrame
    {
        public const int JointCount = 26;
        public const uint AllValid = (1u << JointCount) - 1;
        public uint Sequence;
        public double LocalCaptureTimeSeconds;
        // Wrists are AvatarRoot-relative; joint poses are relative to their own wrist.
        public Pose LeftWrist, RightWrist;
        public uint LeftValidMask, RightValidMask;
        public Pose[] LeftJoints, RightJoints;
        public static bool IsValid(uint mask, int index) => (mask & (1u << index)) != 0;
        public HandJointFrame Copy()
        {
            var copy = this;
            copy.LeftJoints = LeftJoints == null ? null : (Pose[])LeftJoints.Clone();
            copy.RightJoints = RightJoints == null ? null : (Pose[])RightJoints.Clone();
            return copy;
        }
    }
    public interface IHandJointSource
    {
        // true may contain zero valid masks: an explicit invalidation is still a frame.
        bool TryGetLatestHandJoints(out HandJointFrame frame);
    }
}
