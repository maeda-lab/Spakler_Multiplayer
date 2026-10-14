using UnityEngine;

namespace QuestLAN.Simulation
{
    [RequireComponent(typeof(MockPoseSource))]
    public sealed class SandboxPoseControls : MonoBehaviour
    {
        private MockPoseSource source;
        private void Awake() => source = GetComponent<MockPoseSource>();
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(394, 12, 320, 170), GUI.skin.box);
            GUILayout.Label("Local Mock controls");
            source.LeftHandTracked = GUILayout.Toggle(source.LeftHandTracked, "Left wrist (cube)");
            source.RightHandTracked = GUILayout.Toggle(source.RightHandTracked, "Right wrist (capsule)");
            source.Animate = GUILayout.Toggle(source.Animate, "Animate poses");
            source.OutputAvailable = GUILayout.Toggle(source.OutputAvailable, "Output available");
            GUILayout.EndArea();
        }
    }
}
