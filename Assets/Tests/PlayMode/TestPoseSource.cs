using QuestLAN.Shared;
using UnityEngine;

namespace QuestLAN.Tests
{
    // Test fixture only. B's runtime MockPoseSource belongs in Simulation.
    public sealed class TestPoseSource : MonoBehaviour, IAvatarPoseSource
    {
        public AvatarPoseFrame Frame;
        public bool Available = true;
        public bool TryGetLatestPose(out AvatarPoseFrame frame)
        {
            frame = Frame;
            return Available;
        }
    }
}
