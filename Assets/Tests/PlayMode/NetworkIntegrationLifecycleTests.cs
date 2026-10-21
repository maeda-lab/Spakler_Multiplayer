using System.Collections;
using NUnit.Framework;
using QuestLAN.Integration;
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
                session.Disconnect();
                yield return WaitFor(() => !manager.IsListening && !manager.ShutdownInProgress && !binding.IsMapped &&
                    Object.FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None).Length == 0);
                Assert.That(Object.FindObjectsByType<NetworkPoseSync>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(session.LocalPoseSource, Is.Null);
                Assert.That(Vector3.Distance(rig.transform.position, initialPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(rig.transform.rotation, initialRotation), Is.LessThan(0.001f));
            }
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
