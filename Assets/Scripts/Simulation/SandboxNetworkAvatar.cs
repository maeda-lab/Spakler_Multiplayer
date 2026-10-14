using QuestLAN.Avatar;
using QuestLAN.Networking;
using UnityEngine;

namespace QuestLAN.Simulation
{
    [RequireComponent(typeof(NetworkPoseSync))]
    public sealed class SandboxNetworkAvatar : MonoBehaviour
    {
        private void Awake()
        {
            var sync = GetComponent<NetworkPoseSync>();
            var head = Marker("Head", PrimitiveType.Sphere, 0.22f);
            var left = Marker("Left wrist (cube)", PrimitiveType.Cube, 0.12f);
            var right = Marker("Right wrist (capsule)", PrimitiveType.Capsule, 0.1f);
            var visuals = new GameObject("Pose View");
            visuals.transform.SetParent(transform, false);
            visuals.AddComponent<AvatarPoseView>().Configure(sync, transform, head, left, right, true);
        }
        private Transform Marker(string label, PrimitiveType shape, float size)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = label;
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * size;
            Destroy(visual.GetComponent<Collider>());
            visual.SetActive(false);
            return visual.transform;
        }
    }
}
