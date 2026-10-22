using NUnit.Framework;
using QuestLAN.Shared;
using UnityEngine;

namespace QuestLAN.Networking.Tests
{
    public sealed class PoseProtocolTests
    {
        private static AvatarPoseFrame Frame(uint sequence, bool left = true) => new AvatarPoseFrame
        {
            Sequence = sequence, LocalCaptureTimeSeconds = 10,
            Head = new Pose(new Vector3(0, 1.6f, 0), Quaternion.identity),
            LeftHand = new Pose(new Vector3(-0.3f, 1, 0), Quaternion.identity),
            RightHand = new Pose(new Vector3(0.3f, 1, 0), Quaternion.identity),
            HeadTracked = true, LeftHandTracked = left, RightHandTracked = true
        };

        [Test]
        public void RejectsDuplicatesOldFramesAndAcceptsSequenceWrap()
        {
            var buffer = new PoseSnapshotBuffer();
            Assert.That(buffer.Push(Frame(uint.MaxValue), 1), Is.True);
            Assert.That(buffer.Push(Frame(uint.MaxValue), 1.01), Is.False);
            Assert.That(buffer.Push(Frame(uint.MaxValue - 1), 1.02), Is.False);
            Assert.That(buffer.LastArrivalSeconds, Is.EqualTo(1), "Rejected packets must not refresh receive age.");
            Assert.That(buffer.Push(Frame(0), 1.03), Is.True);
            buffer.Reset();
            Assert.That(buffer.LastArrivalSeconds, Is.Zero);
            Assert.That(buffer.Push(Frame(1), 2), Is.True);
        }

        [Test]
        public void TrackingLossIsImmediateAndTimeoutInvalidatesEveryPart()
        {
            var buffer = new PoseSnapshotBuffer();
            buffer.Push(Frame(1), 1);
            buffer.Push(Frame(2, false), 1.05);
            Assert.That(buffer.TrySample(1.06, 0.1, 0.5, out var lost), Is.True);
            Assert.That(lost.LeftHandTracked, Is.False);
            Assert.That(lost.HeadTracked && lost.RightHandTracked, Is.True);
            buffer.TrySample(1.6, 0.1, 0.5, out var timedOut);
            Assert.That(timedOut.HeadTracked || timedOut.LeftHandTracked || timedOut.RightHandTracked, Is.False);
            var restored = Frame(3); restored.LeftHand.position = new Vector3(-1, 1, 0);
            buffer.Push(restored, 1.65);
            buffer.TrySample(1.65, 0.1, 0.5, out var recovered);
            Assert.That(recovered.LeftHandTracked, Is.True);
            Assert.That(recovered.LeftHand.position, Is.EqualTo(restored.LeftHand.position));
        }

        [Test]
        public void InterpolatesWithArrivalClockAndPreservesLeftRightIdentity()
        {
            var buffer = new PoseSnapshotBuffer();
            var a = Frame(1); var b = Frame(2);
            b.LocalCaptureTimeSeconds = 999999;
            b.Head.position.x = 1;
            buffer.Push(a, 1); buffer.Push(b, 1.1);
            buffer.TrySample(1.15, 0.1, 0.5, out var sampled);
            Assert.That(sampled.Head.position.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(sampled.LeftHand.position.x, Is.LessThan(0));
            Assert.That(sampled.RightHand.position.x, Is.GreaterThan(0));
        }

        [Test]
        public void RejectsMalformedTrackedDataAndCanonicalizesInvalidPoses()
        {
            var packet = new NetworkAvatarPosePacket { Version = 1, Frame = Frame(1) };
            packet.Frame.Head.position.x = float.NaN;
            Assert.That(packet.TryValidate(), Is.False);
            packet.Frame.HeadTracked = false;
            Assert.That(packet.TryValidate(), Is.True);
            Assert.That(packet.Frame.Head, Is.EqualTo(Pose.identity));
            packet.Frame.LeftHand.rotation = new Quaternion(0, 0, 0, 2);
            Assert.That(packet.TryValidate(), Is.True);
            Assert.That(packet.Frame.LeftHand.rotation.w, Is.EqualTo(1));
            packet.Version = 2;
            Assert.That(packet.TryValidate(), Is.False);
        }
    }
}
