using System.Collections.Generic;
using QuestLAN.Shared;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.InputSystem;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace QuestLAN.XR
{
    /// <summary>Dynamic wrist snapshots plus HMD pose, captured in LateUpdate. No network calls.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class XRHandsPoseSource : MonoBehaviour, IAvatarPoseSource, IHandJointSource
    {
        [SerializeField] private Transform trackingOrigin;
        [SerializeField] private Transform headCamera;
        [SerializeField] private Transform avatarRoot;


        private static readonly XRHandJointID[] JointIds = { XRHandJointID.Palm, XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip, XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip, XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip, XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip, XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip };
        private readonly Pose[] rawLeft = new Pose[26], rawRight = new Pose[26];
        private readonly Pose[][] jointLeft = { new Pose[26], new Pose[26] }, jointRight = { new Pose[26], new Pose[26] };
        private uint leftMask, rightMask;
        private int jointSlot;
        private HandJointFrame handFrame;
        public bool TryGetLatestHandJoints(out HandJointFrame frame) { frame = handFrame; return hasFrame; }
        private static uint ReadJoints(XRHand hand, bool valid, Pose wrist, Pose[] output)
        {
            if (!valid) return 0;
            uint mask = 0; var inverse = Quaternion.Inverse(wrist.rotation);
            for (int i = 0; i < 26; i++)
                if (hand.GetJoint(JointIds[i]).TryGetPose(out var pose))
                { output[i] = new Pose(inverse * (pose.position - wrist.position), inverse * pose.rotation); mask |= 1u << i; }
            return mask;
        }

        private readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem subsystem;
        private AvatarPoseFrame latest;
        private bool hasFrame;
        private bool paused;
        private uint sequence;
        private int wristFrame = -1;
        private Pose leftWrist;
        private Pose rightWrist;
        private bool leftValid;
        private bool rightValid;
        [SerializeField] private bool controllerFallback;
        [SerializeField] private Vector3 controllerWristOffset = new Vector3(0, 0, -0.04f);
        private InputActionMap controllers;
        public void EnableControllerFallback(bool value) => controllerFallback = value;
        private void EnableControllerActions()
        {
            if (!controllerFallback) return;
            controllers = new InputActionMap("Controller wrist fallback");
            foreach (var side in new[] { "LeftHand", "RightHand" })
            {
                string device = "<XRController>{" + side + "}/";
                controllers.AddAction(side + "Position", InputActionType.Value, device + "devicePosition");
                controllers.AddAction(side + "Rotation", InputActionType.Value, device + "deviceRotation");
                controllers.AddAction(side + "Tracked", InputActionType.Value, device + "isTracked");
                controllers.AddAction(side + "State", InputActionType.Value, device + "trackingState");
            }
            controllers.Enable();
        }

        public static Pose GripToWrist(Pose grip, Vector3 localOffset) =>
            new Pose(grip.position + grip.rotation * localOffset, grip.rotation);

        private bool ReadControllerWrist(XRNode side, out Pose wrist)
        {
            wrist = Pose.identity;
            if (!controllerFallback) return false;
            if (controllers == null) return false;
            string prefix = side == XRNode.LeftHand ? "LeftHand" : "RightHand";
            var tracked = controllers[prefix + "Tracked"];
            if (!(tracked.activeControl?.device is UnityEngine.InputSystem.XR.XRController device) || !device.added ||
                tracked.ReadValue<float>() < 0.5f || (controllers[prefix + "State"].ReadValue<int>() & 3) != 3) return false;
            wrist = GripToWrist(new Pose(controllers[prefix + "Position"].ReadValue<Vector3>(),
                controllers[prefix + "Rotation"].ReadValue<Quaternion>()), controllerWristOffset);
            return true;
        }

        public void Configure(Transform origin, Transform cameraTransform, Transform root)
        {
            trackingOrigin = origin;
            headCamera = cameraTransform;
            avatarRoot = root;
            hasFrame = false;
            wristFrame = -1;
        }

        public bool TryGetLatestPose(out AvatarPoseFrame frame)
        {
            frame = latest;
            return hasFrame;
        }

        private void OnEnable()
        {
            hasFrame = false;
            paused = false;
            wristFrame = -1;
            EnableControllerActions();
            FindSubsystem();
        }

        private void Update()
        {
            if (paused) return;
            if (subsystem == null || !subsystem.running) FindSubsystem();
        }

        private void FindSubsystem()
        {
            Unsubscribe();
            subsystems.Clear();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var candidate in subsystems)
            {
                if (!candidate.running) continue;
                subsystem = candidate;
                subsystem.updatedHands += OnHandsUpdated;
                break;
            }
        }

        private void OnHandsUpdated(XRHandSubsystem sender,
            XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType updateType)
        {
            if (paused || !sender.running || updateType != XRHandSubsystem.UpdateType.Dynamic) return;
            wristFrame = Time.frameCount;
            leftValid = ReadWrist(sender.leftHand,
                (flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0, out leftWrist);
            rightValid = ReadWrist(sender.rightHand,
                (flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0, out rightWrist);
            leftMask = ReadJoints(sender.leftHand, leftValid, leftWrist, rawLeft);
            rightMask = ReadJoints(sender.rightHand, rightValid, rightWrist, rawRight);
        }

        private static bool ReadWrist(XRHand hand, bool jointsUpdated, out Pose wrist)
        {
            wrist = Pose.identity;
            return jointsUpdated && hand.isTracked && hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out wrist);
        }

        private void LateUpdate()
        {
            if (paused) return;
            if (!trackingOrigin || !headCamera || !avatarRoot ||
                !UnitScale(trackingOrigin) || !UnitScale(avatarRoot))
            {
                hasFrame = false;
                return;
            }

            var frame = NewFrame();
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var required = InputTrackingState.Position | InputTrackingState.Rotation;
            frame.HeadTracked = hmd.isValid &&
                hmd.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && tracked &&
                hmd.TryGetFeatureValue(CommonUsages.trackingState, out var state) &&
                (state & required) == required;
            if (frame.HeadTracked)
                frame.Head = ToRoot(new Pose(headCamera.position, headCamera.rotation));

            var fresh = subsystem != null && subsystem.running && wristFrame == Time.frameCount;
            frame.LeftHandTracked = fresh && leftValid;
            frame.RightHandTracked = fresh && rightValid;
            if (frame.LeftHandTracked) frame.LeftHand = TrackingToRoot(leftWrist);
            else if (ReadControllerWrist(XRNode.LeftHand, out var controllerLeft))
            { frame.LeftHandTracked = true; frame.LeftHand = TrackingToRoot(controllerLeft); }
            if (frame.RightHandTracked) frame.RightHand = TrackingToRoot(rightWrist);
            else if (ReadControllerWrist(XRNode.RightHand, out var controllerRight))
            { frame.RightHandTracked = true; frame.RightHand = TrackingToRoot(controllerRight); }
            var jl = jointLeft[jointSlot]; var jr = jointRight[jointSlot]; jointSlot = 1 - jointSlot;
            System.Array.Copy(rawLeft, jl, 26); System.Array.Copy(rawRight, jr, 26);
            handFrame = new HandJointFrame { Sequence = frame.Sequence, LocalCaptureTimeSeconds = frame.LocalCaptureTimeSeconds,
                LeftWrist = fresh && leftValid ? TrackingToRoot(leftWrist) : Pose.identity,
                RightWrist = fresh && rightValid ? TrackingToRoot(rightWrist) : Pose.identity,
                LeftValidMask = fresh && leftValid ? leftMask : 0, RightValidMask = fresh && rightValid ? rightMask : 0,
                LeftJoints = jl, RightJoints = jr };
            latest = frame;
            hasFrame = true;
        }

        private static bool UnitScale(Transform value)
        {
            for (var current = value; current != null; current = current.parent)
                if ((current.localScale - Vector3.one).sqrMagnitude > 0.000001f) return false;
            return true;
        }

        private Pose TrackingToRoot(Pose pose)
        {
            return ToRoot(new Pose(trackingOrigin.TransformPoint(pose.position),
                trackingOrigin.rotation * pose.rotation));
        }

        private Pose ToRoot(Pose world)
        {
            return new Pose(avatarRoot.InverseTransformPoint(world.position),
                Quaternion.Inverse(avatarRoot.rotation) * world.rotation);
        }

        private AvatarPoseFrame NewFrame()
        {
            return new AvatarPoseFrame
            {
                Sequence = unchecked(++sequence),
                LocalCaptureTimeSeconds = Time.realtimeSinceStartupAsDouble,
                Head = Pose.identity,
                LeftHand = Pose.identity,
                RightHand = Pose.identity
            };
        }

        private void Invalidate()
        {
            latest = NewFrame();
            handFrame = new HandJointFrame { Sequence = latest.Sequence, LocalCaptureTimeSeconds = latest.LocalCaptureTimeSeconds };
            hasFrame = true;
            wristFrame = -1;
            leftValid = rightValid = false;
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            Invalidate();
        }

        private void OnDisable()
        {
            controllers?.Dispose(); controllers = null;
            Unsubscribe();
            Invalidate();
        }

        private void Unsubscribe()
        {
            if (subsystem != null) subsystem.updatedHands -= OnHandsUpdated;
            subsystem = null;
            wristFrame = -1;
            leftValid = rightValid = false;
        }
    }
}
