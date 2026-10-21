using QuestLAN.Avatar;
using QuestLAN.Networking;
using Unity.Netcode;
using UnityEngine;

namespace QuestLAN.Integration
{
    public sealed class NetworkAvatarPresentation : NetworkBehaviour
    {
        [SerializeField] private Material pulseMaterial;
        private GameObject visuals;
        private Renderer pulse;
        private Material material;
        public byte AppliedColorIndex { get; private set; }
        public Material PulseMaterial => pulseMaterial;
        public void Configure(Material value) => pulseMaterial = value;
        public static Color PulseColor(byte index) => (index % 4) switch
        { 0 => Color.cyan, 1 => Color.magenta, 2 => Color.yellow, _ => Color.green };

        public override void OnNetworkSpawn()
        {
            visuals = new GameObject("Remote pose visuals");
            visuals.transform.SetParent(transform, false);
            var head = Marker("Remote head", PrimitiveType.Sphere, new Vector3(0.18f, 0.18f, 0.18f));
            var left = Marker("Remote left wrist", PrimitiveType.Cube, new Vector3(0.08f, 0.04f, 0.14f));
            var right = Marker("Remote right wrist", PrimitiveType.Sphere, Vector3.one * 0.08f);
            visuals.AddComponent<AvatarPoseView>().Configure(GetComponent<NetworkPoseSync>(), transform, head, left, right, true);
            visuals.SetActive(!IsOwner); // Local wrists already have the hardware view; never show an owner head.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Synchronized color";
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = new Vector3(0.75f, 1.35f, 1.5f);
            cube.transform.localScale = Vector3.one * 0.15f;
            Destroy(cube.GetComponent<Collider>());
            pulse = cube.GetComponent<Renderer>();
            material = new Material(pulseMaterial);
            pulse.sharedMaterial = material;
        }

        private Transform Marker(string label, PrimitiveType type, Vector3 scale)
        {
            var marker = GameObject.CreatePrimitive(type);
            marker.name = label;
            marker.transform.SetParent(visuals.transform, false);
            marker.transform.localScale = scale;
            Destroy(marker.GetComponent<Collider>());
            marker.SetActive(false);
            return marker.transform;
        }

        private void Update()
        {
            if (!IsSpawned || !material) return;
            AppliedColorIndex = GetComponent<NetworkPlayerSignals>().ColorIndex.Value;
            material.SetColor("_BaseColor", PulseColor(AppliedColorIndex));
        }

        public override void OnNetworkDespawn()
        {
            if (visuals) Destroy(visuals);
            if (pulse) Destroy(pulse.gameObject);
            if (material) Destroy(material);
        }
    }
}
