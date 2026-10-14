using QuestLAN.Avatar;
using UnityEngine;

namespace QuestLAN.Simulation
{
    /// <summary>Local preview only; deliberately offers no simulated network status.</summary>
    [RequireComponent(typeof(MockPoseSource))]
    public sealed class MockAvatarPreview : MonoBehaviour
    {
        private MockPoseSource source;
        private GameObject visuals;

        private void Awake()
        {
            source = GetComponent<MockPoseSource>();
            visuals = new GameObject("Mock Visuals");
            visuals.transform.SetParent(transform, false);
            var head = Marker("Head (sphere)", PrimitiveType.Sphere, new Vector3(0.22f, 0.22f, 0.22f));
            var left = Marker("Left wrist (cube)", PrimitiveType.Cube, new Vector3(0.12f, 0.12f, 0.12f));
            var right = Marker("Right wrist (capsule)", PrimitiveType.Capsule, new Vector3(0.1f, 0.1f, 0.1f));
            visuals.AddComponent<AvatarPoseView>().Configure(source, transform, head, left, right, true);
        }

        private Transform Marker(string label, PrimitiveType type, Vector3 scale)
        {
            var marker = GameObject.CreatePrimitive(type);
            marker.name = label;
            marker.transform.SetParent(visuals.transform, false);
            marker.transform.localScale = scale;
            Destroy(marker.GetComponent<Collider>());
            return marker.transform;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 340, 240), GUI.skin.box);
            GUILayout.Label("Local Mock preview — networking not installed");
            source.LeftHandTracked = GUILayout.Toggle(source.LeftHandTracked, "Left wrist (cube) tracked");
            source.RightHandTracked = GUILayout.Toggle(source.RightHandTracked, "Right wrist (capsule) tracked");
            source.Animate = GUILayout.Toggle(source.Animate, "Animate poses");
            source.OutputAvailable = GUILayout.Toggle(source.OutputAvailable, "Output available");
            if (source.TryGetLatestPose(out var frame))
                GUILayout.Label($"Sequence: {frame.Sequence}\nCapture: {frame.LocalCaptureTimeSeconds:F3}s (local clock)");
            else GUILayout.Label("No available frame");
            GUILayout.EndArea();
        }

        private void OnEnable() { if (visuals) visuals.SetActive(true); }
        private void OnDisable() { if (visuals) visuals.SetActive(false); }
    }
}
