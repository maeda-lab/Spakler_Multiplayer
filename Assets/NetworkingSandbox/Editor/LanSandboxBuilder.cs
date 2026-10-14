using System.IO;
using QuestLAN.Networking;
using QuestLAN.Simulation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuestLAN.NetworkSandbox.Editor
{
    public static class LanSandboxBuilder
    {
        public const string ScenePath = "Assets/NetworkingSandbox/Scenes/LanSandbox.unity";
        [MenuItem("QuestLAN/Networking/Create LAN Sandbox")]
        public static void Create()
        {
            const string playerPath = "Assets/Prefabs/Network/NetworkPlayer.prefab";
            const string sessionPath = "Assets/Prefabs/Network/NetworkSession.prefab";
            const string listPath = "Assets/Prefabs/Network/LanNetworkPrefabs.asset";
            foreach (string path in new[] { ScenePath, playerPath, sessionPath, listPath })
                if (File.Exists(path)) throw new System.InvalidOperationException("LAN assets already exist: " + path);
            Directory.CreateDirectory("Assets/NetworkingSandbox/Scenes");
            Directory.CreateDirectory("Assets/Prefabs/Network");
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var player = new GameObject("NetworkPlayer AvatarRoot");
                player.AddComponent<NetworkObject>();
                player.AddComponent<NetworkPoseSync>();
                player.AddComponent<SandboxNetworkAvatar>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(player, playerPath);
                Object.DestroyImmediate(player);
                ValidatePlayerPrefab(prefab);
                var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                list.Add(new NetworkPrefab { Prefab = prefab });
                AssetDatabase.CreateAsset(list, listPath);
                var session = new GameObject("NetworkSession");
                var transport = session.AddComponent<UnityTransport>();
                var manager = session.AddComponent<NetworkManager>();
                manager.NetworkConfig.NetworkTransport = transport;
                manager.NetworkConfig.PlayerPrefab = prefab;
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
                manager.NetworkConfig.ConnectionApproval = true;
                manager.NetworkConfig.EnableSceneManagement = false;
                manager.NetworkConfig.TickRate = 30;
                var source = session.AddComponent<MockPoseSource>();
                session.AddComponent<LanSessionController>().Configure(source);
                session.AddComponent<SandboxPoseControls>();
                session.AddComponent<LanSmokeProbe>();
                PrefabUtility.SaveAsPrefabAssetAndConnect(session, sessionPath, InteractionMode.AutomatedAction);
                var camera = new GameObject("LAN Sandbox Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 1.4f, 4);
                camera.transform.LookAt(new Vector3(0, 1.3f, 0));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.16f, 0.2f);
                var light = new GameObject("LAN Sandbox Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("LAN sandbox assets generated via Unity. Build Settings unchanged.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void BuildWindows()
        {
            ValidatePlayerPrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkPlayer.prefab"));
            var session = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkSession.prefab");
            var manager = session.GetComponent<NetworkManager>();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/Prefabs/Network/LanNetworkPrefabs.asset"));
            PrefabUtility.SavePrefabAsset(session);
            Directory.CreateDirectory("Build/M2");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/M2/QuestLANSandbox.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Logs/QuestLAN-M2-Build.txt", result.summary.result + " errors=" + result.summary.totalErrors);
        }

        private static void ValidatePlayerPrefab(GameObject prefab)
        {
            // Let NGO compute its hash from Unity's real persistent GlobalObjectId.
            prefab.GetComponent<NetworkObject>().SendMessage("OnValidate", SendMessageOptions.RequireReceiver);
            PrefabUtility.SavePrefabAsset(prefab);
            var serialized = new SerializedObject(prefab.GetComponent<NetworkObject>());
            if (serialized.FindProperty("GlobalObjectIdHash").uintValue == 0)
                throw new System.InvalidOperationException("Unity/NGO did not generate a valid network prefab hash.");
        }
    }
}
