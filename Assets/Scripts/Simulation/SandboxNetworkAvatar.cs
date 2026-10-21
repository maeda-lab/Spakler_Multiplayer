using QuestLAN.Avatar;
using QuestLAN.Networking;
using UnityEngine;

namespace QuestLAN.Simulation
{
    [RequireComponent(typeof(NetworkPoseSync))]
    public sealed class SandboxNetworkAvatar : MonoBehaviour
    {
        [SerializeField] private Material pulseMaterial;
        public void ConfigureMaterial(Material value) => pulseMaterial = value;
        private void Awake()
        {
            var sync = GetComponent<NetworkPoseSync>();
            var head = Marker("Head", PrimitiveType.Sphere, 0.22f);
            var left = Marker("Left wrist (cube)", PrimitiveType.Cube, 0.12f);
            var right = Marker("Right wrist (capsule)", PrimitiveType.Capsule, 0.1f);
            var visuals = new GameObject("Pose View");
            visuals.transform.SetParent(transform, false);
            visuals.AddComponent<AvatarPoseView>().Configure(sync, transform, head, left, right, true);
            var color = GameObject.CreatePrimitive(PrimitiveType.Cube);
            color.name = "Synchronized color";
            color.transform.SetParent(transform, false);
            color.transform.localPosition = new Vector3(0, 0.7f, 0.6f);
            color.transform.localScale = Vector3.one * 0.15f;
            Destroy(color.GetComponent<Collider>());
            colorMaterial = new Material(pulseMaterial);
            color.GetComponent<Renderer>().sharedMaterial = colorMaterial;
        }
        private Material colorMaterial;
        private void Update()
        {
            var signals = GetComponent<NetworkPlayerSignals>();
            if (signals && signals.IsSpawned && colorMaterial)
                colorMaterial.SetColor("_BaseColor", signals.ColorIndex.Value switch
                { 0 => Color.cyan, 1 => Color.magenta, 2 => Color.yellow, _ => Color.green });
        }
        private void OnDestroy() { if (colorMaterial) Destroy(colorMaterial); }
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
