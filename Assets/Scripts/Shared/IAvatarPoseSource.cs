namespace QuestLAN.Shared
{
    public interface IAvatarPoseSource
    {
        /// <summary>
        /// True after a frame has been sampled, even if every tracking flag is false.
        /// Repeated reads may return the same sequence. False means no available frame.
        /// </summary>
        bool TryGetLatestPose(out AvatarPoseFrame frame);
    }
}
