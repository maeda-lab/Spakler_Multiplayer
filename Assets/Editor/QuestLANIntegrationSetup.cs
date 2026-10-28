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

        [MenuItem("QuestLAN/Update head avatar and session controls")]
        public static void UpgradeAvatarAndPanelBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var rig = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            var view = scene.GetRootGameObjects().Single(root => root.name == "Local AvatarRoot")
                .GetComponent<QuestLAN.Avatar.AvatarPoseView>();
            var camera = rig.Camera;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            int layer = LayerMask.NameToLayer("LocalAvatarHead");
            if (layer < 0)
            {
                for (int i = 31; i >= 8; i--)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { layer = i; break; }
                if (layer < 0) throw new InvalidOperationException("No free user layer for the local head.");
                layers.GetArrayElementAtIndex(layer).stringValue = "LocalAvatarHead";
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            var headMaterial = AvatarMaterial("HeadBlue", new Color(0.18f, 0.62f, 0.95f));
            var eyeMaterial = AvatarMaterial("EyesDark", new Color(0.015f, 0.025f, 0.04f));
            const string headPath = "Assets/Prefabs/Visual/HeadAvatar.prefab";
            GameObject headPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(headPath);
            if (!headPrefab)
            {
                var head = new GameObject("HeadAvatar");
                try
                {
                    HeadPart(head.transform, "Head Sphere", Vector3.zero, 0.18f, headMaterial);
                    HeadPart(head.transform, "Left Eye", new Vector3(-0.035f, 0.018f, 0.082f), 0.035f, eyeMaterial);
                    HeadPart(head.transform, "Right Eye", new Vector3(0.035f, 0.018f, 0.082f), 0.035f, eyeMaterial);
                    headPrefab = PrefabUtility.SaveAsPrefabAsset(head, headPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(head); }
            }
            var serializedView = new SerializedObject(view);
            var oldHead = serializedView.FindProperty("headVisual").objectReferenceValue as Transform;
            var localHead = oldHead && oldHead.name == "Local Head Avatar" ? oldHead.gameObject :
                (GameObject)PrefabUtility.InstantiatePrefab(headPrefab, scene);
            localHead.name = "Local Head Avatar";
            localHead.transform.SetParent(view.transform, false);
            localHead.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            foreach (var item in localHead.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = layer;
            camera.cullingMask &= ~(1 << layer); // Other cameras/Scene view can still see the local head.
            serializedView.FindProperty("headVisual").objectReferenceValue = localHead.transform;
            serializedView.FindProperty("showHead").boolValue = true;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            if (oldHead && oldHead.gameObject != localHead) UnityEngine.Object.DestroyImmediate(oldHead.gameObject);
            var product = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                product.GetComponent<NetworkAvatarPresentation>().ConfigureHead(headPrefab);
                PrefabUtility.SaveAsPrefabAsset(product, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(product); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Head avatar prefab, local camera exclusion and remote head configured; English controls ready.");
        }

        private static Material AvatarMaterial(string name, Color color)
        {
            string path = "Assets/Prefabs/Visual/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) throw new InvalidOperationException("URP Unlit shader missing.");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void HeadPart(Transform parent, string name, Vector3 position, float diameter, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = Vector3.one * diameter;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

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
