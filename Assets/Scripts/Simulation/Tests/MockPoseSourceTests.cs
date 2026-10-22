using System.Collections;
using NUnit.Framework;
using QuestLAN.Avatar;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuestLAN.Simulation.Tests
{
    public sealed class MockPoseSourceTests
    {
        private GameObject root;
        [SetUp] public void SetUp() => root = new GameObject("Mock test root");
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator ProcessMemoryCountersDoNotSilentlyReturnZero()
        {
            yield return null;
            LanSmokeProbe.ReadProcessMemory(out long workingSet, out long privateBytes);
            Assert.That(workingSet, Is.GreaterThan(0));
            Assert.That(privateBytes, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ReportReservationRejectsConcurrentAndExistingOutputs()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath,
                "QuestLAN-reservation-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                using (var lease = LanSmokeProbe.ReserveReportOutput(path))
                    Assert.Throws<System.IO.IOException>(() => LanSmokeProbe.ReserveReportOutput(path));
                System.IO.File.WriteAllText(path, "preserve this report");
                Assert.Throws<System.IO.IOException>(() => LanSmokeProbe.ReserveReportOutput(path));
                Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo("preserve this report"));
                Assert.That(System.IO.File.Exists(path + ".run.lock"), Is.False);
            }
            finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LifecycleInvalidatesAndResamplesWithoutReadSideEffects()
        {
            var source = root.AddComponent<MockPoseSource>();
            Assert.That(source.TryGetLatestPose(out _), Is.False);
            yield return null;
            Assert.That(source.TryGetLatestPose(out var frame), Is.True);
            source.TryGetLatestPose(out var repeated);
            Assert.That(repeated.Sequence, Is.EqualTo(frame.Sequence));
            Assert.That(frame.HeadTracked && frame.LeftHandTracked && frame.RightHandTracked, Is.True);
            source.enabled = false;
            Assert.That(source.TryGetLatestPose(out var invalid), Is.True);
            Assert.That(invalid.HeadTracked || invalid.LeftHandTracked || invalid.RightHandTracked, Is.False);
            Assert.That(unchecked((int)(invalid.Sequence - frame.Sequence)), Is.GreaterThan(0));
            source.enabled = true;
            Assert.That(source.TryGetLatestPose(out _), Is.False);
            yield return null;
            Assert.That(source.TryGetLatestPose(out var resumed), Is.True);
            Assert.That(resumed.LocalCaptureTimeSeconds, Is.GreaterThanOrEqualTo(invalid.LocalCaptureTimeSeconds));
            root.SendMessage("OnApplicationPause", true);
            source.TryGetLatestPose(out invalid);
            Assert.That(invalid.HeadTracked || invalid.LeftHandTracked || invalid.RightHandTracked, Is.False);
            root.SendMessage("OnApplicationPause", false);
            Assert.That(source.TryGetLatestPose(out _), Is.False);
        }

        [UnityTest]
        public IEnumerator IndependentTrackingAndUnavailableOutputHideVisualsUnderRotatedRoot()
        {
            root.transform.SetPositionAndRotation(new Vector3(3, 0, 2), Quaternion.Euler(0, 90, 0));
            var source = root.AddComponent<MockPoseSource>();
            source.Animate = false;
            var head = new GameObject("Head");
            var left = new GameObject("Left");
            var right = new GameObject("Right");
            head.transform.SetParent(root.transform);
            left.transform.SetParent(root.transform);
            right.transform.SetParent(root.transform);
            root.AddComponent<AvatarPoseView>().Configure(source, root.transform, head.transform, left.transform, right.transform, true);
            source.LeftHandTracked = false;
            yield return null;
            yield return null;
            Assert.That(left.activeSelf, Is.False);
            Assert.That(right.activeSelf && head.activeSelf, Is.True);
            source.TryGetLatestPose(out var frame);
            Assert.That(Vector3.Distance(right.transform.position, root.transform.TransformPoint(frame.RightHand.position)), Is.LessThan(0.0001f));
            source.RightHandTracked = false;
            source.LeftHandTracked = true;
            yield return null;
            yield return null;
            Assert.That(left.activeSelf, Is.True);
            Assert.That(right.activeSelf, Is.False);
            source.OutputAvailable = false;
            yield return null;
            yield return null;
            Assert.That(left.activeSelf || right.activeSelf || head.activeSelf, Is.False);
        }
    }
}
