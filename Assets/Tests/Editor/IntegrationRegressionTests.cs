using System.Linq;
using NUnit.Framework;
using QuestLAN.Integration;
using QuestLAN.Networking;
using QuestLAN.Simulation;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuestLAN.EditorTests
{
    public sealed class IntegrationRegressionTests
    {
        [Test]
        public void RttUsesLocalElapsedTimeAndRejectsDuplicateOrLateReplies()
        {
            var rtt = new RttTracker();
            var first = rtt.Begin(10);
            Assert.That(rtt.Complete(first + 1, 10.1), Is.False);
            Assert.That(rtt.Complete(first, 10.1), Is.True);
            Assert.That(rtt.CurrentMilliseconds, Is.EqualTo(100).Within(0.001));
            Assert.That(rtt.Complete(first, 10.2), Is.False);
            var second = rtt.Begin(12);
            rtt.Tick(13.1);
            Assert.That(rtt.TimedOut, Is.True);
            Assert.That(rtt.Complete(second, 13.2), Is.False);
            var third = rtt.Begin(14);
            Assert.That(rtt.Complete(third, 14.2), Is.True);
            Assert.That(rtt.TimedOut, Is.False);
            Assert.That(rtt.SmoothedMilliseconds, Is.EqualTo(120).Within(0.001));
            rtt.Reset();
            Assert.That(rtt.Complete(third, 15), Is.False);
            Assert.That(rtt.HasSample, Is.False);
            var stalled = rtt.Begin(20);
            Assert.That(rtt.Complete(stalled, 21.1), Is.False, "A late reply is rejected even if Update has not ticked.");
            Assert.That(rtt.TimedOut, Is.True);
        }

        [Test]
        public void SpawnMappingPreservesRootRelativeHeadAndWristAcrossRotation()
        {
            var oldRoot = new GameObject("Old root"); var newRoot = new GameObject("Network root");
            var rig = new GameObject("Rig"); var offset = new GameObject("Floor offset");
            try
            {
                oldRoot.transform.SetPositionAndRotation(new Vector3(2, 0, 1), Quaternion.Euler(0, 30, 0));
                newRoot.transform.SetPositionAndRotation(new Vector3(-0.65f, 0, 0), Quaternion.Euler(0, 90, 0));
                rig.transform.SetPositionAndRotation(oldRoot.transform.position, oldRoot.transform.rotation);
                offset.transform.SetParent(rig.transform, false); offset.transform.localPosition = new Vector3(0, 1.1f, 0);
                var wrist = new Vector3(-0.3f, 0.2f, 0.5f);
                var expected = oldRoot.transform.InverseTransformPoint(offset.transform.TransformPoint(wrist));
                XRNetworkBinding.AlignRig(rig.transform, oldRoot.transform, newRoot.transform);
                var actual = newRoot.transform.InverseTransformPoint(offset.transform.TransformPoint(wrist));
                Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.0001f));
                newRoot.transform.localScale = Vector3.one * 2;
                Assert.Throws<System.InvalidOperationException>(() => XRNetworkBinding.AlignRig(rig.transform, oldRoot.transform, newRoot.transform));
            }
            finally { Object.DestroyImmediate(rig); Object.DestroyImmediate(oldRoot); Object.DestroyImmediate(newRoot); }
        }

        [Test]
        public void MainSceneHasProductNetworkingAndNoMockHardwareSources()
        {
            Assert.That(UnityEditor.PlayerSettings.Android.forceInternetPermission, Is.True);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Assert.That(objects.Count(item => item.GetComponent<XROrigin>()), Is.EqualTo(1));
            Assert.That(objects.Count(item => item.GetComponent<XRNetworkBinding>()), Is.EqualTo(1));
            Assert.That(objects.Count(item => item.GetComponent<VRSessionPanel>()), Is.EqualTo(1));
            Assert.That(objects.Any(item => item.GetComponent<MockPoseSource>() || item.GetComponent<LanSmokeProbe>() || item.GetComponent<SandboxPoseControls>()), Is.False);
            var session = objects.Select(item => item.GetComponent<LanSessionController>()).Single(item => item);
            Assert.That(session.LocalPoseSource, Is.Null, "Real source must be bound only after mapping the spawned root.");
            var player = session.GetComponent<NetworkManager>().NetworkConfig.PlayerPrefab;
            Assert.That(player.GetComponent<NetworkPlayerSignals>(), Is.Not.Null);
            Assert.That(player.GetComponent<NetworkAvatarPresentation>(), Is.Not.Null);
            Assert.That(player.GetComponent<NetworkAvatarPresentation>().PulseMaterial, Is.Not.Null);
            Assert.That(player.GetComponent<NetworkAvatarPresentation>().PulseMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(player.GetComponent<SandboxNetworkAvatar>(), Is.Null);
            var headPrefab = player.GetComponent<NetworkAvatarPresentation>().HeadAvatarPrefab;
            Assert.That(headPrefab, Is.Not.Null);
            Assert.That(headPrefab.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(3));
            Assert.That(headPrefab.transform.Find("Left Eye").localPosition.z, Is.GreaterThan(0));
            Assert.That(headPrefab.transform.Find("Right Eye").localPosition.z, Is.GreaterThan(0));
            Assert.That(headPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            var localHead = objects.Single(item => item.name == "Local Head Avatar");
            int localLayer = LayerMask.NameToLayer("LocalAvatarHead");
            Assert.That(localLayer, Is.GreaterThanOrEqualTo(8));
            Assert.That(localHead.GetComponentsInChildren<Transform>(true).All(item => item.gameObject.layer == localLayer), Is.True);
            var camera = objects.Select(item => item.GetComponent<Camera>()).Single(item => item);
            Assert.That(camera.cullingMask & (1 << localLayer), Is.Zero, "Local head must not obscure first-person VR.");
            Assert.That(player.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(player.GetComponentsInChildren<XROrigin>(true), Is.Empty);
        }
    }
}
