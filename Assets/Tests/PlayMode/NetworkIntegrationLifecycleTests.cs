using System.Collections;
using NUnit.Framework;
using QuestLAN.Integration;
using QuestLAN.Avatar;
using UnityEngine.UI;
using QuestLAN.Networking;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuestLAN.Tests
{
    public sealed class NetworkIntegrationLifecycleTests
    {
        private Scene scene;
        private NetworkManager manager;

        private static IEnumerator WaitFor(System.Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Network lifecycle timed out.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MainSceneHostCanStopAndRestartWithoutStaleMappingOrPlayers()
        {
            scene = SceneManager.GetSceneByName("MainScene");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("MainScene");
            }
            var binding = Object.FindFirstObjectByType<XRNetworkBinding>();
            var rig = Object.FindFirstObjectByType<XROrigin>();
            var session = binding.GetComponent<LanSessionController>();
            manager = session.GetComponent<NetworkManager>();
            var initialPosition = rig.transform.position;
            var initialRotation = rig.transform.rotation;
            for (int round = 0; round < 2; round++)
            {
                Assert.That(session.StartHost("127.0.0.1", 17785), Is.True);
                yield return WaitFor(() => binding.IsMapped);
                Assert.That(Object.FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Vector3.Distance(rig.transform.position, manager.LocalClient.PlayerObject.transform.position), Is.LessThan(0.0001f));
                Assert.That(session.LocalPoseSource, Is.Not.Null);
                // Exercise the same physical gaze path as the headset, including Canvas geometry.
                Canvas panelCanvas = null;
                foreach (var canvas in rig.GetComponentsInChildren<Canvas>())
                    if (canvas.name == "LAN gaze panel") panelCanvas = canvas;
                Assert.That(panelCanvas, Is.Not.Null);
                var colorButton = (RectTransform)panelCanvas.transform.Find("Color");
                var signals = manager.LocalClient.PlayerObject.GetComponent<NetworkPlayerSignals>();
                int requests = signals.ColorRequestsSent;
                byte color = signals.ColorIndex.Value;
                var cameraPosition = rig.transform.position + Vector3.up * 1.4f;
                rig.Camera.transform.SetPositionAndRotation(cameraPosition,
                    Quaternion.LookRotation(colorButton.position - cameraPosition));
                yield return WaitFor(() => signals.ColorRequestsSent > requests);
                yield return WaitFor(() => signals.ColorIndex.Value != color);
                Assert.That(signals.ColorIndex.Value, Is.Not.EqualTo(color), "Gaze must change server color, not only button highlight.");
                yield return null;
                Assert.That(signals.GetComponent<NetworkAvatarPresentation>().AppliedColorIndex, Is.EqualTo(signals.ColorIndex.Value));
                rig.Camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                yield return null;
                Assert.That(session.PlayerCount, Is.EqualTo(1));
                var sessionPanel = binding.GetComponent<VRSessionPanel>();
                Assert.That(sessionPanel.StatusText, Does.Contain("Players: 1/2"));
                Assert.That(panelCanvas.transform.Find("Join").GetComponent<Button>().interactable, Is.False);
                // Stop through the real world-space control, then verify the next Host can start.
                panelCanvas.transform.Find("Disconnect").GetComponent<Button>().onClick.Invoke();
                yield return WaitFor(() => !manager.IsListening && !manager.ShutdownInProgress && !binding.IsMapped &&
                    Object.FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None).Length == 0);
                Assert.That(Object.FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(session.PlayerCount, Is.Zero);
                Assert.That(session.LocalPoseSource, Is.Null);
                Assert.That(Vector3.Distance(rig.transform.position, initialPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(rig.transform.rotation, initialRotation), Is.LessThan(0.001f));
            }
        }

        [UnityTest]
        public IEnumerator HeadEyesFollowCameraPoseAndHideWhenTrackingIsLost()
        {
            scene = SceneManager.GetSceneByName("MainScene");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("MainScene");
            }
            yield return null; // World-space controls are created in Start. Keep one product scene only.
            var binding = Object.FindFirstObjectByType<XRNetworkBinding>();
            manager = binding.GetComponent<NetworkManager>();
            var prefab = manager.NetworkConfig.PlayerPrefab.GetComponent<NetworkAvatarPresentation>().HeadAvatarPrefab;
            var owner = new GameObject("Head pose fixture");
            SceneManager.MoveGameObjectToScene(owner, scene);
            owner.transform.SetPositionAndRotation(new Vector3(2, 0, -3), Quaternion.Euler(0, 60, 0));
            var source = owner.AddComponent<TestPoseSource>();
            var head = Object.Instantiate(prefab, owner.transform).transform;
            owner.AddComponent<AvatarPoseView>().Configure(source, owner.transform, head, null, null, true);
            var camera = Object.FindFirstObjectByType<XROrigin>().Camera;
            for (int i = 0; i < 2; i++)
            {
                camera.transform.SetPositionAndRotation(new Vector3(i, 1.6f, 0.3f), Quaternion.Euler(10, 35 + i * 70, 0));
                source.Frame.HeadTracked = true;
                source.Frame.Head = new Pose(owner.transform.InverseTransformPoint(camera.transform.position),
                    Quaternion.Inverse(owner.transform.rotation) * camera.transform.rotation);
                yield return null; yield return null;
                Assert.That(head.gameObject.activeSelf, Is.True);
                Assert.That(Vector3.Distance(head.position, camera.transform.position), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(head.rotation, camera.transform.rotation), Is.LessThan(0.001f));
                var eyes = (head.Find("Left Eye").position + head.Find("Right Eye").position) * 0.5f;
                Assert.That(Vector3.Dot((eyes - head.position).normalized, camera.transform.forward), Is.GreaterThan(0.95f));
                source.Frame.HeadTracked = false;
                yield return null; yield return null;
                Assert.That(head.gameObject.activeSelf, Is.False);
            }
            source.Available = false;
            yield return null; yield return null;
            Assert.That(head.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator EnglishJoinControlsValidateInputAndCancelPendingClient()
        {
            scene = SceneManager.GetSceneByName("MainScene");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("MainScene");
            }
            yield return null; // World-space controls are created in Start. Keep one product scene only.
            var panel = Object.FindFirstObjectByType<VRSessionPanel>();
            var session = panel.GetComponent<LanSessionController>();
            manager = panel.GetComponent<NetworkManager>();
            var canvas = Object.FindFirstObjectByType<XROrigin>().GetComponentInChildren<Canvas>();
            // All labels, including validation feedback, must use the bundled Latin font.
            foreach (var text in canvas.GetComponentsInChildren<Text>())
                foreach (char ch in text.text) Assert.That(ch, Is.LessThan((char)128));
            var join = canvas.transform.Find("Join").GetComponent<Button>();
            session.HostAddress = "invalid";
            join.onClick.Invoke();
            yield return null;
            Assert.That(manager.IsListening, Is.False);
            Assert.That(session.Status, Does.StartWith("Failed:"));
            session.HostAddress = "127.0.0.1";
            session.PortText = "0";
            join.onClick.Invoke();
            yield return null;
            Assert.That(panel.StatusText, Does.Contain("Enter a UDP port"));
            Assert.That(manager.IsListening, Is.False);
            session.PortText = "17889";
            join.onClick.Invoke();
            yield return null;
            Assert.That(session.CanStart, Is.False);
            Assert.That(session.CanDisconnect, Is.True);
            canvas.transform.Find("Disconnect").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => session.CanStart && !manager.ShutdownInProgress);
            Assert.That(session.PlayerCount, Is.Zero);
            canvas.transform.Find("Show Status").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(panel.StatusText, Does.Contain("Players: 0/2"));
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (manager)
            {
                manager.Shutdown();
                yield return WaitFor(() => !manager || !manager.ShutdownInProgress);
                if (manager) Object.Destroy(manager.gameObject);
            }
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1) SceneManager.CreateScene("After integration lifecycle");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
    }
}
