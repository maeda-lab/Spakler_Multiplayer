using System;
using UnityEngine;

namespace QuestLAN.Shared
{
    /// <summary>Contract v1. All poses are relative to the sender's unit-scale AvatarRoot.</summary>
    [Serializable]
    public struct AvatarPoseFrame
    {
        public uint Sequence;
        public double LocalCaptureTimeSeconds;
        public Pose Head;
        public Pose LeftHand;
        public Pose RightHand;
        public bool HeadTracked;
        public bool LeftHandTracked;
        public bool RightHandTracked;
    }
}
