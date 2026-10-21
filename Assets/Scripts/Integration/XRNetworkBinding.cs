using QuestLAN.Networking;
using QuestLAN.XR;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace QuestLAN.Integration
{
    /// <summary>Owns the mapping between one hardware rig and its spawned network root.</summary>
    [DefaultExecutionOrder(-250)]
    public sealed class XRNetworkBinding : MonoBehaviour
    {
        [SerializeField] private XROrigin rig;
        [SerializeField] private XRHandsPoseSource source;
        [SerializeField] private Transform localRoot;
        private LanSessionController session;
        private NetworkManager manager;
        private NetworkPoseSync bound;
        private Pose originalRig, originalRoot;
        private bool mapped;
        public bool IsMapped => mapped;
        public void Configure(XROrigin origin, XRHandsPoseSource poseSource, Transform root)
        { rig = origin; source = poseSource; localRoot = root; }

        private void Awake()
        {
            session = GetComponent<LanSessionController>();
            manager = GetComponent<NetworkManager>();
            originalRig = new Pose(rig.transform.position, rig.transform.rotation);
            originalRoot = new Pose(localRoot.position, localRoot.rotation);
            session.Configure(null); // No valid sample can leave before the spawn mapping is ready.
        }

        private void Update()
        {
            var player = manager.IsConnectedClient ? manager.LocalClient?.PlayerObject : null;
            var candidate = player ? player.GetComponent<NetworkPoseSync>() : null;
            if (candidate == bound && (candidate || !mapped)) return;
            RestoreLocalMapping();
            if (!candidate || !candidate.IsOwner || !candidate.IsSpawned) return;
            AlignRig(rig.transform, localRoot, candidate.transform);
            source.Configure(rig.CameraFloorOffsetObject.transform, rig.Camera.transform, candidate.transform);
            bound = candidate; mapped = true;
            session.Configure(source);
            bound.BindLocalSource(source);
        }

        public static void AlignRig(Transform rigTransform, Transform previousRoot, Transform targetRoot)
        {
            RequireUnitScale(rigTransform); RequireUnitScale(previousRoot); RequireUnitScale(targetRoot);
            var rotation = targetRoot.rotation * Quaternion.Inverse(previousRoot.rotation);
            var position = targetRoot.TransformPoint(previousRoot.InverseTransformPoint(rigTransform.position));
            rigTransform.SetPositionAndRotation(position, rotation * rigTransform.rotation);
            previousRoot.SetPositionAndRotation(targetRoot.position, targetRoot.rotation);
        }

        private static void RequireUnitScale(Transform value)
        {
            for (var current = value; current; current = current.parent)
                if ((current.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                    throw new System.InvalidOperationException("XR/network mapping requires unit scale: " + current.name);
        }

        private void RestoreLocalMapping()
        {
            if (!session) return;
            session.Configure(null);
            if (bound && bound.IsSpawned && bound.IsOwner) bound.BindLocalSource(null);
            bound = null;
            if (!mapped) return;
            mapped = false;
            if (!rig || !localRoot || !source) return;
            rig.transform.SetPositionAndRotation(originalRig.position, originalRig.rotation);
            localRoot.SetPositionAndRotation(originalRoot.position, originalRoot.rotation);
            source.Configure(rig.CameraFloorOffsetObject.transform, rig.Camera.transform, localRoot);
        }

        private void OnDisable() => RestoreLocalMapping();
    }
}
