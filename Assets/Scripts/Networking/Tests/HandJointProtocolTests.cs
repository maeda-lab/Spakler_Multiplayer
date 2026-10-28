using NUnit.Framework;
using QuestLAN.Shared;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;

namespace QuestLAN.Networking.Tests
{
    public sealed class HandJointProtocolTests
    {
        [Test]
        public void FullHandsRoundTripFitsUnreliablePayloadAndPreservesQuantizedPoses()
        {
            var packet = new NetworkHandJointPacket { Version = 1, Frame = Frame(123) };
            packet.Frame.LeftJoints[10] = new Pose(new Vector3(-0.0567f, 0.0333f, 0.1214f), Quaternion.Euler(35, 65, -20));
            using var writer = new FastBufferWriter(1000, Allocator.Temp);
            writer.WriteNetworkSerializable(packet);
            Assert.That(writer.Length, Is.EqualTo(NetworkHandJointPacket.MaximumPayloadBytes));
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out NetworkHandJointPacket result);
            Assert.That(result.TryValidate(), Is.True);
            Assert.That(result.Frame.Sequence, Is.EqualTo(123));
            Assert.That(Vector3.Distance(result.Frame.LeftJoints[10].position, packet.Frame.LeftJoints[10].position), Is.LessThan(0.0009f));
            Assert.That(Quaternion.Angle(result.Frame.LeftJoints[10].rotation.normalized, packet.Frame.LeftJoints[10].rotation), Is.LessThan(0.05f));
        }
        private static HandJointFrame Frame(uint sequence)
        {
            var joints = new Pose[26];
            for (int i = 0; i < 26; i++) joints[i] = new Pose(new Vector3(0, 0, i * 0.005f), Quaternion.identity);
            return new HandJointFrame { Sequence = sequence, LocalCaptureTimeSeconds = 5, LeftValidMask = HandJointFrame.AllValid,
                RightValidMask = HandJointFrame.AllValid, LeftWrist = new Pose(Vector3.left, Quaternion.identity),
                RightWrist = new Pose(Vector3.right, Quaternion.identity), LeftJoints = joints, RightJoints = (Pose[])joints.Clone() };
        }
        [Test]
        public void RejectsUnknownBitsMissingWristInvalidJointsAndVersions()
        {
            var packet = new NetworkHandJointPacket { Version = 1, Frame = Frame(1) };
            Assert.That(packet.TryValidate(), Is.True);
            packet.Frame.LeftValidMask |= 1u << 30; Assert.That(packet.TryValidate(), Is.False);
            packet.Frame.LeftValidMask = 1; Assert.That(packet.TryValidate(), Is.False);
            packet.Frame.LeftValidMask = HandJointFrame.AllValid;
            packet.Frame.LeftJoints[5].position.x = float.NaN; Assert.That(packet.TryValidate(), Is.False);
            packet.Frame.LeftValidMask = 0; Assert.That(packet.TryValidate(), Is.True, "Invalid joints are not consumed");
            packet.Version = 2; Assert.That(packet.TryValidate(), Is.False);
        }
        [Test]
        public void JointLossTimeoutRecoverySequenceWrapAndProducerReuseAreSafe()
        {
            var buffer = new HandJointSnapshotBuffer(); var a = Frame(uint.MaxValue);
            Assert.That(buffer.Push(a, 1), Is.True);
            a.LeftJoints[5].position.x = 0.9f;
            buffer.TrySample(1, 0.1, 0.5, out var copy);
            Assert.That(copy.LeftJoints[5].position.x, Is.Zero, "Buffer owns received arrays");
            var b = Frame(0); b.LeftValidMask &= ~(1u << 5);
            Assert.That(buffer.Push(b, 1.1), Is.True);
            Assert.That(buffer.Push(b, 1.4), Is.False);
            buffer.TrySample(1.11, 0.1, 0.5, out var partial);
            Assert.That(HandJointFrame.IsValid(partial.LeftValidMask, 5), Is.False);
            Assert.That(partial.RightValidMask, Is.EqualTo(HandJointFrame.AllValid));
            buffer.TrySample(1.61, 0.1, 0.5, out var timeout);
            Assert.That(timeout.LeftValidMask | timeout.RightValidMask, Is.Zero);
            buffer.Push(Frame(1), 1.7); buffer.TrySample(1.7, 0.1, 0.5, out var recovered);
            Assert.That(recovered.LeftValidMask, Is.EqualTo(HandJointFrame.AllValid));
            Assert.That(recovered.LeftJoints[5].position.z, Is.EqualTo(0.025f).Within(0.0001));
        }
        [Test]
        public void ArrivalInterpolationDoesNotUseOtherMachinesCaptureClock()
        {
            var buffer = new HandJointSnapshotBuffer(); var a = Frame(1); var b = Frame(2);
            b.LocalCaptureTimeSeconds = 99999; b.LeftJoints[5].position.x = 0.1f;
            buffer.Push(a, 1); buffer.Push(b, 1.1); buffer.TrySample(1.15, 0.1, 0.5, out var result);
            Assert.That(result.LeftJoints[5].position.x, Is.EqualTo(0.05f).Within(0.0001));
            Assert.That(result.RightWrist.position.x, Is.EqualTo(1));
        }
    }
}
