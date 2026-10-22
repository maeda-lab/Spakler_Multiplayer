using QuestLAN.Shared;
using UnityEngine;

namespace QuestLAN.Networking
{
    /// <summary>Bounded arrival-time buffer. Never compares clocks from different devices.</summary>
    public sealed class PoseSnapshotBuffer
    {
        private struct Snapshot { public AvatarPoseFrame Frame; public double Arrival; }
        private readonly Snapshot[] samples = new Snapshot[16];
        private int count;
        private uint lastSequence;
        private bool hasSequence;
        private double lastArrival;
        public int AcceptedCount { get; private set; }
        public uint LastSequence => lastSequence;
        public double LastArrivalSeconds => lastArrival;

        public static bool IsNewer(uint candidate, uint previous) => unchecked((int)(candidate - previous)) > 0;

        public void Reset()
        {
            count = 0;
            hasSequence = false;
            lastSequence = 0;
            lastArrival = 0;
            AcceptedCount = 0;
        }

        public bool Push(AvatarPoseFrame frame, double arrival)
        {
            if (hasSequence && !IsNewer(frame.Sequence, lastSequence)) return false;
            if (hasSequence && arrival - lastArrival > 0.5) count = 0;
            if (count == samples.Length)
            {
                System.Array.Copy(samples, 1, samples, 0, count - 1);
                count--;
            }
            samples[count++] = new Snapshot { Frame = frame, Arrival = arrival };
            lastArrival = arrival;
            lastSequence = frame.Sequence;
            hasSequence = true;
            AcceptedCount++;
            return true;
        }

        public bool TrySample(double now, double interpolationDelay, double timeout, out AvatarPoseFrame frame)
        {
            frame = default;
            if (count == 0) return false;
            var latest = samples[count - 1].Frame;
            if (now - lastArrival > timeout)
            {
                frame = latest;
                frame.HeadTracked = frame.LeftHandTracked = frame.RightHandTracked = false;
                return true;
            }
            double target = now - interpolationDelay;
            int index = 0;
            while (index + 1 < count && samples[index + 1].Arrival <= target) index++;
            frame = samples[index].Frame;
            if (index + 1 < count)
            {
                var a = samples[index];
                var b = samples[index + 1];
                float alpha = (float)System.Math.Clamp((target - a.Arrival) / System.Math.Max(0.000001, b.Arrival - a.Arrival), 0, 1);
                Blend(ref frame.Head, a.Frame.Head, b.Frame.Head, a.Frame.HeadTracked && b.Frame.HeadTracked, alpha);
                Blend(ref frame.LeftHand, a.Frame.LeftHand, b.Frame.LeftHand, a.Frame.LeftHandTracked && b.Frame.LeftHandTracked, alpha);
                Blend(ref frame.RightHand, a.Frame.RightHand, b.Frame.RightHand, a.Frame.RightHandTracked && b.Frame.RightHandTracked, alpha);
            }
            // Immediately invalidate/reinitialize a body part across a tracking boundary.
            ApplyValidity(ref frame.Head, ref frame.HeadTracked, latest.Head, latest.HeadTracked, 0, index);
            ApplyValidity(ref frame.LeftHand, ref frame.LeftHandTracked, latest.LeftHand, latest.LeftHandTracked, 1, index);
            ApplyValidity(ref frame.RightHand, ref frame.RightHandTracked, latest.RightHand, latest.RightHandTracked, 2, index);
            return true;
        }

        private void ApplyValidity(ref Pose pose, ref bool tracked, Pose latestPose, bool latestTracked, int part, int first)
        {
            if (!latestTracked) { tracked = false; return; }
            for (int i = count - 1; i >= first; i--)
            {
                var value = samples[i].Frame;
                bool valid = part == 0 ? value.HeadTracked : part == 1 ? value.LeftHandTracked : value.RightHandTracked;
                if (!valid) { pose = latestPose; break; }
            }
            if (!tracked) pose = latestPose;
            tracked = true;
        }

        private static void Blend(ref Pose destination, Pose a, Pose b, bool valid, float alpha)
        {
            if (!valid) return;
            destination = new Pose(Vector3.Lerp(a.position, b.position, alpha), Quaternion.Slerp(a.rotation, b.rotation, alpha));
        }
    }
}
