using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuestLAN.Avatar;
using QuestLAN.Shared;
using QuestLAN.XR;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;

namespace QuestLAN.Tests
{
    public sealed class XRTrackingLifecycleTests
    {
        private const string ProviderId = "QuestLAN.Tests.ControlledHands";
        private GameObject owner;
        private Transform origin;
        private Transform camera;
        private Transform root;
        private Transform left;
        private Transform right;
        private XRHandSubsystem hands;
        private XRHandsPoseSource source;

        private Transform Child(string name, Transform parent)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            return result;
        }

        [SetUp]
        public void SetUp()
        {
            var existing = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(existing);
            Assert.That(existing.Any(item => item.running), Is.False,
                "Use QuestLAN/Run isolated XR acceptance tests; hardware loader must not start during synthetic tests.");
            var descriptors = new List<XRHandSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);
            if (descriptors.All(item => item.id != ProviderId))
            {
                XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo
                {
                    id = ProviderId, providerType = typeof(ControlledHandProvider)
                });
                SubsystemManager.GetSubsystemDescriptors(descriptors);
            }
            hands = descriptors.Single(item => item.id == ProviderId).Create();
            ControlledHandProvider.LeftTracked = ControlledHandProvider.RightTracked = true;
            ControlledHandProvider.LeftWristValid = true;
            ControlledHandProvider.LeftWrist = new Pose(new Vector3(-0.3f, 1.2f, 0.4f), Quaternion.Euler(12, 24, 36));
            ControlledHandProvider.RightWrist = new Pose(new Vector3(0.3f, 1.1f, 0.5f), Quaternion.Euler(5, 10, 15));
            hands.Start();
            owner = new GameObject("XR Acceptance Test");
            origin = Child("Camera Floor Offset", owner.transform);
            camera = Child("Camera", origin);
            root = Child("AvatarRoot", owner.transform);
            source = owner.AddComponent<XRHandsPoseSource>();
            source.Configure(origin, camera, root);
            left = Child("Left Visual", root);
            right = Child("Right Visual", root);
            var view = owner.AddComponent<AvatarPoseView>();
            view.Configure(source, root, null, left, right);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (source) source.enabled = false;
            if (hands != null) { hands.Stop(); hands.Destroy(); }
            if (owner) Object.Destroy(owner);
            yield return null;
        }

        private IEnumerator Capture(XRHandSubsystem.UpdateType type = XRHandSubsystem.UpdateType.Dynamic)
        {
            yield return null; // Allow Update to discover a restarted subsystem.
            hands.TryUpdateHands(type);
            yield return null; // Inspect the frame published by the preceding LateUpdate.
        }

        private AvatarPoseFrame Latest()
        {
            Assert.That(source.TryGetLatestPose(out var frame), Is.True);
            return frame;
        }

        [UnityTest]
        public IEnumerator IndependentHandLossHidesOnlyThatHandAndRecoveryUsesNewPose()
        {
            yield return Capture();
            Assert.That(Latest().LeftHandTracked && Latest().RightHandTracked, Is.True);
            ControlledHandProvider.LeftTracked = false;
            yield return Capture();
            Assert.That(Latest().LeftHandTracked, Is.False);
            Assert.That(Latest().RightHandTracked, Is.True);
            Assert.That(left.gameObject.activeSelf, Is.False);
            Assert.That(right.gameObject.activeSelf, Is.True);
            ControlledHandProvider.LeftTracked = true;
            ControlledHandProvider.LeftWrist = new Pose(new Vector3(-0.8f, 1.4f, 0.7f), Quaternion.Euler(10, 40, 20));
            yield return Capture();
            Assert.That(Latest().LeftHandTracked, Is.True);
            Assert.That(left.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(left.position, origin.TransformPoint(ControlledHandProvider.LeftWrist.position)), Is.LessThan(0.0001f));
            ControlledHandProvider.RightTracked = false;
            yield return Capture();
            Assert.That(Latest().LeftHandTracked, Is.True);
            Assert.That(Latest().RightHandTracked, Is.False);
            Assert.That(left.gameObject.activeSelf, Is.True);
            Assert.That(right.gameObject.activeSelf, Is.False);
            ControlledHandProvider.RightTracked = true;
            yield return Capture();
            Assert.That(Latest().RightHandTracked && right.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator MissingWristOrMissingDynamicUpdateNeverReusesValidPose()
        {
            yield return Capture();
            Assert.That(Latest().LeftHandTracked, Is.True);
            ControlledHandProvider.LeftWristValid = false;
            yield return Capture();
            Assert.That(hands.leftHand.isTracked, Is.True);
            Assert.That(Latest().LeftHandTracked, Is.False);
            Assert.That(Latest().RightHandTracked, Is.True);
            yield return null; // No Dynamic callback in the preceding frame.
            Assert.That(Latest().LeftHandTracked || Latest().RightHandTracked, Is.False);
            ControlledHandProvider.LeftWristValid = true;
            yield return Capture(XRHandSubsystem.UpdateType.BeforeRender);
            Assert.That(Latest().LeftHandTracked || Latest().RightHandTracked, Is.False);
            yield return Capture();
            Assert.That(Latest().LeftHandTracked && Latest().RightHandTracked, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseStopAndRepeatedReenableInvalidateThenRecoverWithoutDuplicateSubscriptions()
        {
            yield return Capture();
            Assert.That(Latest().LeftHandTracked, Is.True);
            source.SendMessage("OnApplicationPause", true);
            Assert.That(Latest().HeadTracked || Latest().LeftHandTracked || Latest().RightHandTracked, Is.False);
            yield return Capture();
            Assert.That(Latest().LeftHandTracked || Latest().RightHandTracked, Is.False);
            source.SendMessage("OnApplicationPause", false);
            Assert.That(Latest().LeftHandTracked || Latest().RightHandTracked, Is.False);
            yield return Capture();
            Assert.That(Latest().LeftHandTracked, Is.True);
            hands.Stop();
            yield return null;
            yield return null;
            Assert.That(Latest().LeftHandTracked || Latest().RightHandTracked, Is.False);
            hands.Start();
            yield return Capture();
            Assert.That(Latest().LeftHandTracked, Is.True);
            for (var cycle = 0; cycle < 5; ++cycle)
            {
                source.enabled = false;
                Assert.That(Latest().LeftHandTracked || Latest().RightHandTracked, Is.False);
                Assert.That(hands.updatedHands == null ||
                    hands.updatedHands.GetInvocationList().All(callback => !ReferenceEquals(callback.Target, source)), Is.True);
                source.enabled = true;
                Assert.That(source.TryGetLatestPose(out _), Is.False);
                yield return Capture();
                Assert.That(Latest().LeftHandTracked && Latest().RightHandTracked, Is.True);
                Assert.That(hands.updatedHands.GetInvocationList().Count(callback => ReferenceEquals(callback.Target, source)), Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator WristRoundTripPreservesWorldPoseAcrossRotatedRootsAndFloorDeviceOffsets()
        {
            owner.transform.SetPositionAndRotation(new Vector3(4, 0, -3), Quaternion.Euler(0, 35, 0));
            origin.localRotation = Quaternion.Euler(0, -25, 0);
            root.SetPositionAndRotation(new Vector3(-2, 0, 6), Quaternion.Euler(0, 110, 0));
            foreach (var height in new[] { 0f, 1.6f })
            {
                origin.localPosition = new Vector3(0.2f, height, -0.1f);
                yield return Capture();
                var frame = Latest();
                var expectedPosition = origin.TransformPoint(ControlledHandProvider.LeftWrist.position);
                var expectedRotation = origin.rotation * ControlledHandProvider.LeftWrist.rotation;
                Assert.That(frame.LeftHandTracked, Is.True);
                Assert.That(Vector3.Distance(root.TransformPoint(frame.LeftHand.position), expectedPosition), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(left.position, expectedPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(root.rotation * frame.LeftHand.rotation, expectedRotation), Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(left.rotation, expectedRotation), Is.LessThan(0.01f));
            }
        }
    }
}
