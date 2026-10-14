using System.Collections.Generic;
using QuestLAN.Shared;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace QuestLAN.XR
{
    /// <summary>Dynamic wrist snapshots plus HMD pose, captured in LateUpdate. No network calls.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class XRHandsPoseSource : MonoBehaviour, IAvatarPoseSource
    {
        [SerializeField] private Transform trackingOrigin;
        [SerializeField] private Transform headCamera;
        [SerializeField] private Transform avatarRoot;

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
            if (frame.RightHandTracked) frame.RightHand = TrackingToRoot(rightWrist);
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
