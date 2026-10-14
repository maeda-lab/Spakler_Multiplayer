using QuestLAN.Shared;
using UnityEngine;

namespace QuestLAN.Simulation
{
    /// <summary>Hardware-free contract v1 source. Transform never changes local pose semantics.</summary>
    [DisallowMultipleComponent]
    public sealed class MockPoseSource : MonoBehaviour, IAvatarPoseSource
    {
        [SerializeField] private bool headTracked = true;
        [SerializeField] private bool leftHandTracked = true;
        [SerializeField] private bool rightHandTracked = true;
        [SerializeField] private bool animate = true;
        [SerializeField] private bool outputAvailable = true;
        private AvatarPoseFrame latest;
        private uint sequence;
        private bool hasFrame;
        private bool paused;
        private double startTime;

        public bool Animate { get => animate; set => animate = value; }
        public bool OutputAvailable
        {
            get => outputAvailable;
            set
            {
                if (outputAvailable == value) return;
                outputAvailable = value;
                hasFrame = false;
            }
        }
        public bool LeftHandTracked { get => leftHandTracked; set => leftHandTracked = value; }
        public bool RightHandTracked { get => rightHandTracked; set => rightHandTracked = value; }

        public bool TryGetLatestPose(out AvatarPoseFrame frame)
        {
            frame = latest;
            // Disabled/paused sources expose their explicit invalidation, per Shared v1.
            return hasFrame && (!isActiveAndEnabled || paused || outputAvailable);
        }

        private void OnEnable()
        {
            hasFrame = false;
            startTime = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            if (paused || !outputAvailable) return;
            float phase = animate ? (float)(Time.realtimeSinceStartupAsDouble - startTime) : 0f;
            float wave = Mathf.Sin(phase * 2f);
            latest = new AvatarPoseFrame
            {
                Sequence = unchecked(++sequence),
                LocalCaptureTimeSeconds = Time.realtimeSinceStartupAsDouble,
                Head = new Pose(new Vector3(0, 1.65f, 0), Quaternion.Euler(0, wave * 15f, 0)),
                LeftHand = new Pose(new Vector3(-0.3f, 1.1f + wave * 0.15f, 0.35f), Quaternion.Euler(wave * 30f, 0, 0)),
                RightHand = new Pose(new Vector3(0.3f, 1.1f - wave * 0.15f, 0.35f), Quaternion.Euler(-wave * 30f, 0, 0)),
                HeadTracked = headTracked,
                LeftHandTracked = leftHandTracked,
                RightHandTracked = rightHandTracked
            };
            hasFrame = true;
        }

        private void Invalidate()
        {
            latest.Sequence = unchecked(++sequence);
            latest.LocalCaptureTimeSeconds = Time.realtimeSinceStartupAsDouble;
            latest.HeadTracked = latest.LeftHandTracked = latest.RightHandTracked = false;
            hasFrame = true;
        }

        private void OnDisable() => Invalidate();
        private void OnApplicationPause(bool pause)
        {
            paused = pause;
            if (pause) Invalidate();
            else hasFrame = false;
        }
    }
}
