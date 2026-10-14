using System.IO;
using QuestLAN.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuestLAN.NetworkSandbox.Editor
{
    public static class NetworkSandboxBuilder
    {
        [MenuItem("QuestLAN/Networking/Create Local Mock Sandbox")]
        public static void Create()
        {
            const string scenePath = "Assets/NetworkingSandbox/Scenes/NetworkSandbox.unity";
            const string prefabPath = "Assets/Prefabs/Network/MockAvatar.prefab";
            if (File.Exists(scenePath) || File.Exists(prefabPath))
                throw new System.InvalidOperationException("Sandbox assets already exist. Open them; creation will not overwrite them.");
            Directory.CreateDirectory("Assets/NetworkingSandbox/Scenes");
            Directory.CreateDirectory("Assets/Prefabs/Network");
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var avatar = new GameObject("Mock AvatarRoot (local preview)");
                avatar.AddComponent<MockPoseSource>();
                avatar.AddComponent<MockAvatarPreview>();
                PrefabUtility.SaveAsPrefabAssetAndConnect(avatar, prefabPath, InteractionMode.AutomatedAction);
                var camera = new GameObject("Sandbox Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 1.4f, 3);
                camera.transform.LookAt(new Vector3(0, 1.3f, 0));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.16f, 0.2f);
                var light = new GameObject("Sandbox Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log("Created local-only Mock sandbox. No NetworkManager or XR Rig.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
