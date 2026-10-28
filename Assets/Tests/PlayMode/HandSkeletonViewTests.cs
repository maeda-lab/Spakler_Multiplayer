using System.Collections;
using NUnit.Framework;
using QuestLAN.Avatar;
using QuestLAN.Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuestLAN.Tests
{
    public sealed class TestHandJointSource : MonoBehaviour, IHandJointSource
    {
        public HandJointFrame Frame;
        public bool TryGetLatestHandJoints(out HandJointFrame frame) { frame = Frame; return true; }
    }
    public sealed class HandSkeletonViewTests
    {
        [UnityTest]
        public IEnumerator ReconstructsWristRelativeJointsAndHidesLostOrDisabledSkeleton()
        {
            var root = new GameObject("Joint test root");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(4, 0, 2), Quaternion.Euler(0, 90, 0));
                var source = root.AddComponent<TestHandJointSource>();
                var joints = new Pose[26]; for (int i = 0; i < 26; i++) joints[i] = Pose.identity;
                joints[10] = new Pose(Vector3.forward * 0.1f, Quaternion.identity);
                source.Frame = new HandJointFrame { LeftWrist = new Pose(new Vector3(-0.3f, 1, 0), Quaternion.Euler(0, 30, 0)),
                    LeftJoints = joints, LeftValidMask = (1u << 1) | (1u << 10) };
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube); fallback.transform.SetParent(root.transform);
                var view = root.AddComponent<HandSkeletonView>(); view.Configure(source, root.transform, null, fallback.transform, null);
                yield return null; yield return null;
                Assert.That(view.LeftValidCount, Is.EqualTo(2)); Assert.That(view.RightValidCount, Is.Zero);
                var tip = root.transform.Find("Left joint skeleton/IndexTip");
                var expected = root.transform.TransformPoint(source.Frame.LeftWrist.position + source.Frame.LeftWrist.rotation * joints[10].position);
                Assert.That(Vector3.Distance(tip.position, expected), Is.LessThan(0.0001f));
                Assert.That(fallback.GetComponent<Renderer>().enabled, Is.False, "Static model must not imitate live fingers");
                source.Frame.LeftValidMask = 0; yield return null;
                Assert.That(tip.gameObject.activeInHierarchy, Is.False);
                source.Frame.LeftValidMask = (1u << 1) | (1u << 10); yield return null;
                Assert.That(tip.gameObject.activeInHierarchy, Is.True);
                view.enabled = false;
                Assert.That(tip.gameObject.activeInHierarchy, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
