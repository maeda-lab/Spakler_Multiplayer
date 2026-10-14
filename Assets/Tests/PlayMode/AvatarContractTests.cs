using System.Collections;
using NUnit.Framework;
using QuestLAN.Avatar;
using QuestLAN.Shared;
using QuestLAN.XR;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuestLAN.Tests
{
    public sealed class AvatarContractTests
    {
        private GameObject owner;
        private Transform Child(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(owner.transform, false);
            return child;
        }

        [SetUp] public void SetUp() => owner = new GameObject("Test Owner");
        [UnityTearDown] public IEnumerator TearDown()
        {
            Object.Destroy(owner);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ViewRebuildsRotatedRootAndHidesOnlyInvalidHandAndLocalHead()
        {
            owner.transform.SetPositionAndRotation(new Vector3(3, 0, 2), Quaternion.Euler(0, 90, 0));
            var source = owner.AddComponent<TestPoseSource>();
            var head = Child("Head");
            var left = Child("Left");
            var right = Child("Right");
            var view = owner.AddComponent<AvatarPoseView>();
            view.Configure(source, owner.transform, head, left, right);
            source.Frame = new AvatarPoseFrame
            {
                HeadTracked = true,
                LeftHandTracked = true,
                RightHandTracked = false,
                LeftHand = new Pose(new Vector3(0.2f, 1, 0.5f), Quaternion.Euler(30, 10, 0))
            };
            yield return null;
            yield return null;
            Assert.That(head.gameObject.activeSelf, Is.False);
            Assert.That(left.gameObject.activeSelf, Is.True);
            Assert.That(right.gameObject.activeSelf, Is.False);
            Assert.That(Vector3.Distance(left.position, owner.transform.TransformPoint(source.Frame.LeftHand.position)), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(left.rotation, owner.transform.rotation * source.Frame.LeftHand.rotation), Is.LessThan(0.01f));
            source.Frame.LeftHandTracked = false;
            source.Frame.RightHandTracked = true;
            source.Frame.RightHand = Pose.identity;
            yield return null;
            yield return null;
            Assert.That(left.gameObject.activeSelf, Is.False);
            Assert.That(right.gameObject.activeSelf, Is.True);
            source.Available = false;
            yield return null;
            yield return null;
            Assert.That(right.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SourceWithoutHardwarePublishesInvalidFrameAndRepeatedReadDoesNotAdvance()
        {
            var camera = Child("Camera");
            var source = owner.AddComponent<XRHandsPoseSource>();
            source.Configure(owner.transform, camera, owner.transform);
            Assert.That(source.TryGetLatestPose(out _), Is.False);
            yield return null;
            yield return null;
            Assert.That(source.TryGetLatestPose(out var frame), Is.True);
            Assert.That(frame.HeadTracked || frame.LeftHandTracked || frame.RightHandTracked, Is.False);
            source.TryGetLatestPose(out var repeated);
            Assert.That(repeated.Sequence, Is.EqualTo(frame.Sequence));
            source.enabled = false;
            Assert.That(source.TryGetLatestPose(out var stopped), Is.True);
            Assert.That(stopped.HeadTracked || stopped.LeftHandTracked || stopped.RightHandTracked, Is.False);
            Assert.That(stopped.Sequence, Is.Not.EqualTo(frame.Sequence));
            source.enabled = true;
            Assert.That(source.TryGetLatestPose(out _), Is.False);
        }

        [UnityTest]
        public IEnumerator SourceRejectsMissingReferencesAndScaledAncestor()
        {
            var source = owner.AddComponent<XRHandsPoseSource>();
            yield return null;
            yield return null;
            Assert.That(source.TryGetLatestPose(out _), Is.False);
            var origin = Child("Origin");
            var camera = Child("Camera");
            source.Configure(origin, camera, owner.transform);
            owner.transform.localScale = new Vector3(2, 1, 1);
            yield return null;
            yield return null;
            Assert.That(source.TryGetLatestPose(out _), Is.False);
        }
    }
}
