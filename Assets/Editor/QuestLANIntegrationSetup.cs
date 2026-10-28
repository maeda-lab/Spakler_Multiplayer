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

        [MenuItem("QuestLAN/Set up fixed locomotion landmarks")]
        public static void SetupLocomotionLandmarksBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var old = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Room Landmarks");
            if (old) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("Room Landmarks");
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int i = -25; i <= 25; i++)
            {
                float width = i % 5 == 0 ? 0.045f : 0.015f;
                foreach (var along in new[] { Vector3.forward, Vector3.right })
                {
                    var side = Vector3.Cross(Vector3.up, along);
                    var center = side * i + Vector3.up * 0.008f;
                    int index = vertices.Count;
                    vertices.Add(center - side * width * 0.5f - along * 25);
                    vertices.Add(center - side * width * 0.5f + along * 25);
                    vertices.Add(center + side * width * 0.5f + along * 25);
                    vertices.Add(center + side * width * 0.5f - along * 25);
                    triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
                }
            }
            const string meshPath = "Assets/Prefabs/Visual/RoomGrid.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (!mesh) { mesh = new Mesh { name = "Fixed room grid" }; AssetDatabase.CreateAsset(mesh, meshPath); }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var grid = new GameObject("World floor grid", typeof(MeshFilter), typeof(MeshRenderer));
            grid.transform.SetParent(root.transform, false); grid.GetComponent<MeshFilter>().sharedMesh = mesh;
            grid.GetComponent<MeshRenderer>().sharedMaterial = AvatarMaterial("RoomGrid", new Color(0.1f, 0.2f, 0.26f));
            int number = 0;
            foreach (float x in new[] { -4f, 4f }) foreach (float z in new[] { -4f, 4f })
                HandPart(root.transform, "Fixed marker " + (++number), new Vector3(x, 0.6f, z), new Vector3(0.18f, 1.2f, 0.18f),
                    Quaternion.identity, AvatarMaterial("RoomMarker", new Color(1, 0.55f, 0.1f)));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        [MenuItem("QuestLAN/Set up desktop lobby and locomotion")]
        public static void SetupLobbyAndLocomotionBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var rig = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            var binding = UnityEngine.Object.FindFirstObjectByType<XRNetworkBinding>();
            var move = rig.GetComponent<HeadRelativeLocomotion>();
            if (!move) move = rig.gameObject.AddComponent<HeadRelativeLocomotion>();
            move.Configure(rig.transform, rig.Camera.transform, binding);
            rig.GetComponent<XRHandsPoseSource>().EnableControllerFallback(true);
            rig.Camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            binding.GetComponent<LanSessionController>().DesktopControlsVisible = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(binding.GetComponent<LanSessionController>());
            var left = CreateHandPrefab("LeftHandAvatar", -1);
            var right = CreateHandPrefab("RightHandAvatar", 1);
            var root = scene.GetRootGameObjects().Single(item => item.name == "Local AvatarRoot");
            var view = new SerializedObject(root.GetComponent<QuestLAN.Avatar.AvatarPoseView>());
            foreach (var pair in new[] { ("leftHandVisual", left, "Local Left Hand"), ("rightHandVisual", right, "Local Right Hand") })
            {
                var old = view.FindProperty(pair.Item1).objectReferenceValue as Transform;
                var hand = old && old.name == pair.Item3 ? old.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(pair.Item2, scene);
                hand.name = pair.Item3; hand.transform.SetParent(root.transform, false);
                view.FindProperty(pair.Item1).objectReferenceValue = hand.transform;
                if (old && old.gameObject != hand) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            view.ApplyModifiedPropertiesWithoutUndo();
            var product = PrefabUtility.LoadPrefabContents(PlayerPath);
            try { product.GetComponent<NetworkAvatarPresentation>().ConfigureHands(left, right); PrefabUtility.SaveAsPrefabAsset(product, PlayerPath); }
            finally { PrefabUtility.UnloadPrefabContents(product); }
            var official = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<Transform>(true)).FirstOrDefault(item => item.name == "Official HandVisualizer");
            if (official) official.gameObject.SetActive(false);
            var floor = scene.GetRootGameObjects().First(item => item.name == "Shared Floor");
            floor.transform.localScale = Vector3.one * 5; // Flat 50m floor; travel is bounded to 20m from spawn.
            if (!UnityEngine.Object.FindFirstObjectByType<DesktopRoomFlow>()) new GameObject("Desktop Room Flow", typeof(DesktopRoomFlow));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var settings = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            settings.InitManagerOnStart = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            EditorUtility.SetDirty(settings);
            var lobby = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Desktop Room Flow", typeof(DesktopRoomFlow));
            EditorSceneManager.SaveScene(lobby, "Assets/Scenes/DesktopLobby.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/DesktopLobby.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainScene.unity", true),
                new EditorBuildSettingsScene("Assets/NetworkingSandbox/Scenes/LanSandbox.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Desktop lobby, deferred XR, controller hands and head-relative locomotion configured.");
        }

        private static GameObject CreateHandPrefab(string name, int side)
        {
            string path = "Assets/Prefabs/Visual/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var material = AvatarMaterial("HandsWarm", new Color(0.86f, 0.66f, 0.44f));
            var hand = new GameObject(name);
            try
            {
                HandPart(hand.transform, "Palm", new Vector3(0, 0, 0.045f), new Vector3(0.075f, 0.026f, 0.09f), Quaternion.identity, material);
                float[] lengths = { 0.07f, 0.085f, 0.08f, 0.06f };
                for (int i = 0; i < 4; i++)
                    HandPart(hand.transform, "Finger " + (i + 1), new Vector3(-0.029f + i * 0.019f, 0, 0.09f + lengths[i] * 0.5f),
                        new Vector3(0.014f, 0.022f, lengths[i]), Quaternion.identity, material);
                HandPart(hand.transform, "Thumb", new Vector3(side * 0.052f, -0.005f, 0.045f),
                    new Vector3(0.022f, 0.023f, 0.055f), Quaternion.Euler(0, side * 45, 0), material);
                return PrefabUtility.SaveAsPrefabAsset(hand, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(hand); }
        }
        private static void HandPart(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localRotation = rotation; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material; UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

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
