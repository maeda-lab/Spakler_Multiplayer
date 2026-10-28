using QuestLAN.Shared;
using UnityEngine;

namespace QuestLAN.Avatar
{
    /// <summary>Provider-independent valid joints only. No fabricated fingers for controller grips.</summary>
    public sealed class HandSkeletonView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour source;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Material material;
        [SerializeField] private Transform leftFallback, rightFallback;
        private Transform[] roots = new Transform[2];
        private Transform[][] dots = { new Transform[26], new Transform[26] };
        private LineRenderer[][] bones = { new LineRenderer[26], new LineRenderer[26] };
        public int LeftValidCount { get; private set; }
        public int RightValidCount { get; private set; }
        public void Configure(MonoBehaviour value, Transform root, Material valueMaterial, Transform left, Transform right)
        { source = value; avatarRoot = root; material = valueMaterial; leftFallback = left; rightFallback = right; }
        public void BindSource(MonoBehaviour value) => source = value;
        public static int ParentIndex(int index) => index == 1 ? -1 : index == 0 || index == 2 || index == 6 || index == 11 || index == 16 || index == 21 ? 1 : index - 1;
        private void OnDisable()
        {
            LeftValidCount = RightValidCount = 0;
            foreach (var root in roots) if (root) root.gameObject.SetActive(false);
        }
        private void OnEnable()
        {
            for (int h = 0; h < 2; h++) if (roots[h])
            {
                for (int i = 0; i < 26; i++) { dots[h][i].gameObject.SetActive(false); bones[h][i].enabled = false; }
                roots[h].gameObject.SetActive(true);
            }
        }
        private void Start()
        {
            foreach (var fallback in new[] { leftFallback, rightFallback })
                if (fallback) foreach (var renderer in fallback.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            for (int h = 0; h < 2; h++)
            {
                roots[h] = new GameObject(h == 0 ? "Left joint skeleton" : "Right joint skeleton").transform;
                roots[h].SetParent(transform, false);
                for (int i = 0; i < 26; i++)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.name = ((HandJointIndex)i).ToString();
                    Destroy(dot.GetComponent<Collider>()); dot.transform.SetParent(roots[h], false); dot.transform.localScale = Vector3.one * 0.012f;
                    dot.GetComponent<Renderer>().sharedMaterial = material; dots[h][i] = dot.transform;
                    var bone = new GameObject("Bone " + i).AddComponent<LineRenderer>(); bone.transform.SetParent(roots[h], false);
                    bone.sharedMaterial = material; bone.useWorldSpace = false; bone.positionCount = 2; bone.startWidth = bone.endWidth = 0.006f;
                    bones[h][i] = bone; dot.SetActive(false); bone.enabled = false;
                }
            }
        }
        private void LateUpdate()
        {
            HandJointFrame frame = default;
            bool valid = source && source is IHandJointSource value && value.TryGetLatestHandJoints(out frame) && avatarRoot;
            LeftValidCount = Draw(0, valid ? frame.LeftValidMask : 0, frame.LeftWrist, frame.LeftJoints);
            RightValidCount = Draw(1, valid ? frame.RightValidMask : 0, frame.RightWrist, frame.RightJoints);
        }
        private int Draw(int h, uint mask, Pose wrist, Pose[] poses)
        {
            if (!roots[h]) return 0;
            if (poses == null || poses.Length != 26) mask = 0;
            if (mask != 0) roots[h].SetPositionAndRotation(avatarRoot.TransformPoint(wrist.position), avatarRoot.rotation * wrist.rotation);
            int count = 0;
            for (int i = 0; i < 26; i++)
            {
                bool valid = HandJointFrame.IsValid(mask, i); dots[h][i].gameObject.SetActive(valid);
                if (valid) { count++; dots[h][i].localPosition = poses[i].position; dots[h][i].localRotation = poses[i].rotation; }
                int parent = ParentIndex(i); var bone = bones[h][i];
                bone.enabled = valid && parent >= 0 && HandJointFrame.IsValid(mask, parent);
                if (bone.enabled) { bone.SetPosition(0, poses[parent].position); bone.SetPosition(1, poses[i].position); }
            }
            return count;
        }
    }
}
