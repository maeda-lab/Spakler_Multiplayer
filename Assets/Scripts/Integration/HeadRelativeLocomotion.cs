using QuestLAN.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using QuestLAN.XR;

namespace QuestLAN.Integration
{
    // Move only the tracking rig. Spawn roots remain fixed, so the existing root-relative
    // head/wrist snapshots carry locomotion to the other peer without a second transform stream.
    [DefaultExecutionOrder(-150)]
    public sealed class HeadRelativeLocomotion : MonoBehaviour
    {
        [SerializeField] private Transform rig;
        [SerializeField] private Transform head;
        [SerializeField] private XRNetworkBinding binding;
        [SerializeField, Range(0.1f, 4f)] private float speed = 1.5f;
        [SerializeField, Range(0, 0.8f)] private float deadzone = 0.15f;
        [SerializeField, Range(1, 20)] private float travelRadius = 20;
        private bool mapped, paused;
        private InputAction move;
        private void OnEnable()
        {
            move = new InputAction("Left stick move", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis", expectedControlType: "Vector2");
            move.Enable();
        }
        private void OnDisable() { move?.Dispose(); move = null; mapped = false; }
        private Vector3 start;
        public int MovementSamples { get; private set; }
        public void Configure(Transform origin, Transform camera, XRNetworkBinding networkBinding)
        { rig = origin; head = camera; binding = networkBinding; }

        public static Vector3 Displacement(Vector2 stick, Quaternion headRotation, float metersPerSecond, float delta, float threshold = 0.15f)
        {
            if (!float.IsFinite(stick.x) || !float.IsFinite(stick.y) || !float.IsFinite(delta) || delta <= 0) return Vector3.zero;
            stick = Vector2.ClampMagnitude(stick, 1);
            float magnitude = stick.magnitude;
            if (magnitude <= threshold) return Vector3.zero;
            stick = stick.normalized * Mathf.Clamp01((magnitude - threshold) / (1 - threshold));
            var forward = Vector3.ProjectOnPlane(headRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Quaternion.Euler(0, headRotation.eulerAngles.y, 0) * Vector3.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            return (right * stick.x + forward * stick.y) * metersPerSecond * Mathf.Min(delta, 0.05f);
        }

        private void Update()
        {
            if (!binding || !binding.IsMapped) { mapped = false; return; }
            if (!mapped) { mapped = true; start = rig.position; }
            if (paused || !Application.isFocused || !rig || !head) return;
            var flow = DesktopRoomFlow.Instance;
            if (flow && !flow.RoomEntered) return;
            var source = rig.GetComponent<XRHandsPoseSource>();
            if (!source || !source.TryGetLatestPose(out var pose) || !pose.HeadTracked || move == null ||
                !(move.activeControl?.device is UnityEngine.InputSystem.XR.XRController controller) || !controller.isTracked.isPressed) return;
            var stick = move.ReadValue<Vector2>();
            var step = Displacement(stick, head.rotation, speed, Time.unscaledDeltaTime, deadzone);
            if (step.sqrMagnitude < 0.0000001f) return;
            var target = rig.position + step;
            var offset = Vector2.ClampMagnitude(new Vector2(target.x - start.x, target.z - start.z), travelRadius);
            rig.position = new Vector3(start.x + offset.x, rig.position.y, start.z + offset.y);
            MovementSamples++;
        }
        private void OnApplicationPause(bool value) => paused = value;
    }
}
