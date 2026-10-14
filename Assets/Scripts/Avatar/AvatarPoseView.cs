using QuestLAN.Shared;
using UnityEngine;

namespace QuestLAN.Avatar
{
    /// <summary>Views must be dedicated visuals, never the source rig/camera or an ancestor.</summary>
    [DisallowMultipleComponent]
    public sealed class AvatarPoseView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour poseSource;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Transform headVisual;
        [SerializeField] private Transform leftHandVisual;
        [SerializeField] private Transform rightHandVisual;
        [SerializeField] private bool showHead;

        public void Configure(MonoBehaviour source, Transform root, Transform head,
            Transform left, Transform right, bool displayHead = false)
        {
            Hide();
            poseSource = source;
            avatarRoot = root;
            headVisual = head;
            leftHandVisual = left;
            rightHandVisual = right;
            showHead = displayHead;
        }

        private void LateUpdate()
        {
            if (!poseSource || !avatarRoot || !(poseSource is IAvatarPoseSource source) ||
                !source.TryGetLatestPose(out var frame))
            {
                Hide();
                return;
            }
            Apply(headVisual, frame.Head, frame.HeadTracked && showHead);
            Apply(leftHandVisual, frame.LeftHand, frame.LeftHandTracked);
            Apply(rightHandVisual, frame.RightHand, frame.RightHandTracked);
        }

        private void Apply(Transform visual, Pose pose, bool tracked)
        {
            if (!visual) return;
            visual.gameObject.SetActive(tracked);
            if (tracked)
                visual.SetPositionAndRotation(avatarRoot.TransformPoint(pose.position),
                    avatarRoot.rotation * pose.rotation);
        }

        private void Hide()
        {
            if (headVisual) headVisual.gameObject.SetActive(false);
            if (leftHandVisual) leftHandVisual.gameObject.SetActive(false);
            if (rightHandVisual) rightHandVisual.gameObject.SetActive(false);
        }

        private void OnEnable() => Hide();
        private void OnDisable() => Hide();
    }
}
