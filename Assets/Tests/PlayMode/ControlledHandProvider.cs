using Unity.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;

namespace QuestLAN.Tests
{
    // Test-only provider using the official XR Hands provider API; never installed in a player build.
    [Preserve]
    public sealed class ControlledHandProvider : XRHandSubsystemProvider
    {
        public static bool LeftTracked = true;
        public static bool RightTracked = true;
        public static bool LeftWristValid = true;
        public static Pose LeftWrist = Pose.identity;
        public static Pose RightWrist = Pose.identity;

        public override void Start() { }
        public override void Stop() { }
        public override void Destroy() { }

        public override void GetHandLayout(NativeArray<bool> layout)
        {
            for (var index = 0; index < layout.Length; ++index)
                layout[index] = index == XRHandJointID.Wrist.ToIndex();
        }

        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(
            XRHandSubsystem.UpdateType updateType, ref Pose leftRoot, NativeArray<XRHandJoint> leftJoints,
            ref Pose rightRoot, NativeArray<XRHandJoint> rightJoints)
        {
            // Intentionally different from the wrist: detects accidentally publishing rootPose.
            leftRoot = rightRoot = new Pose(new Vector3(99, 99, 99), Quaternion.identity);
            var wrist = XRHandJointID.Wrist.ToIndex();
            leftJoints[wrist] = XRHandProviderUtility.CreateJoint(Handedness.Left,
                LeftTracked && LeftWristValid ? XRHandJointTrackingState.Pose : XRHandJointTrackingState.None,
                XRHandJointID.Wrist, LeftWrist);
            rightJoints[wrist] = XRHandProviderUtility.CreateJoint(Handedness.Right,
                RightTracked ? XRHandJointTrackingState.Pose : XRHandJointTrackingState.None,
                XRHandJointID.Wrist, RightWrist);
            var flags = XRHandSubsystem.UpdateSuccessFlags.None;
            if (LeftTracked) flags |= XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose |
                XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints;
            if (RightTracked) flags |= XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose |
                XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;
            return flags;
        }
    }
}
