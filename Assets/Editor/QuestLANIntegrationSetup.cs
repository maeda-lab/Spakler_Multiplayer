using System;
using System.IO;
using System.Linq;
using QuestLAN.Integration;
using QuestLAN.Networking;
using QuestLAN.Simulation;
using QuestLAN.XR;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuestLAN.Editor
{
    public static class QuestLANIntegrationSetup
    {
        private const string PlayerPath = "Assets/Prefabs/Visual/XRNetworkPlayer.prefab";
        [MenuItem("QuestLAN/Integrate XR LAN into MainScene")]
        public static void IntegrateBatch()
        {
            PlayerSettings.Android.forceInternetPermission = true;
            Directory.CreateDirectory("Assets/Prefabs/Visual");
            AssetDatabase.Refresh();
            const string materialPath = "Assets/Prefabs/Visual/NetworkPulse.mat";
            var pulseMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!pulseMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) throw new InvalidOperationException("URP Unlit shader is required.");
                pulseMaterial = new Material(shader);
                AssetDatabase.CreateAsset(pulseMaterial, materialPath);
            }
            // B's desktop sandbox keeps its mock and gains the same signals as the product player.
            const string sandboxPath = "Assets/Prefabs/Network/NetworkPlayer.prefab";
            var sandbox = PrefabUtility.LoadPrefabContents(sandboxPath);
            try
            {
                if (!sandbox.GetComponent<NetworkPlayerSignals>()) sandbox.AddComponent<NetworkPlayerSignals>();
                sandbox.GetComponent<SandboxNetworkAvatar>().ConfigureMaterial(pulseMaterial);
                PrefabUtility.SaveAsPrefabAsset(sandbox, sandboxPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(sandbox); }

            if (!File.Exists(PlayerPath))
            {
                var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sandboxPath));
                player.name = "XRNetworkPlayer";
                UnityEngine.Object.DestroyImmediate(player.GetComponent<SandboxNetworkAvatar>());
                player.AddComponent<NetworkAvatarPresentation>();
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
                UnityEngine.Object.DestroyImmediate(player);
            }
            var product = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            product.GetComponent<NetworkAvatarPresentation>().Configure(pulseMaterial);
            product.GetComponent<NetworkObject>().SendMessage("OnValidate", SendMessageOptions.RequireReceiver);
            PrefabUtility.SavePrefabAsset(product);
            if (new SerializedObject(product.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").uintValue == 0)
                throw new InvalidOperationException("NGO must generate a persistent prefab hash.");
            const string listPath = "Assets/Prefabs/Visual/XRNetworkPrefabs.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
            if (!list)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                list.Add(new NetworkPrefab { Prefab = product });
                AssetDatabase.CreateAsset(list, listPath);
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var roots = scene.GetRootGameObjects();
            if (!roots.Any(root => root.GetComponent<XRNetworkBinding>()))
            {
                var rig = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                var source = rig.GetComponent<XRHandsPoseSource>();
                var localRoot = roots.Single(root => root.name == "Local AvatarRoot").transform;
                var session = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkSession.prefab"), scene);
                session.name = "XR NetworkSession";
                UnityEngine.Object.DestroyImmediate(session.GetComponent<SandboxPoseControls>());
                UnityEngine.Object.DestroyImmediate(session.GetComponent<LanSmokeProbe>());
                UnityEngine.Object.DestroyImmediate(session.GetComponent<MockPoseSource>());
                session.GetComponent<LanSessionController>().Configure(null);
                var manager = session.GetComponent<NetworkManager>();
                manager.NetworkConfig.PlayerPrefab = product;
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
                session.AddComponent<XRNetworkBinding>().Configure(rig, source, localRoot);
                session.AddComponent<VRSessionPanel>().Configure(rig.Camera, rig.transform);
                foreach (var component in session.GetComponents<Component>())
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("XR LAN integrated with one hardware rig and a separate remote visual prefab.");
        }
    }
}
