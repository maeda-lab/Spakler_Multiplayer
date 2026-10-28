using System.Linq;
using QuestLAN.Avatar;
using QuestLAN.Networking;
using QuestLAN.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace QuestLAN.Editor
{
    public static class QuestLANHandJointSetup
    {
        [MenuItem("QuestLAN/Set up detailed network hands")]
        public static void RunBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var root = scene.GetRootGameObjects().Single(item => item.name == "Local AvatarRoot");
            var view = root.GetComponent<HandSkeletonView>();
            if (!view) view = root.AddComponent<HandSkeletonView>();
            view.Configure(Object.FindFirstObjectByType<XRHandsPoseSource>(), root.transform,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Visual/HandsWarm.mat"),
                root.transform.Find("Local Left Hand"), root.transform.Find("Local Right Hand"));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            const string path = "Assets/Prefabs/Visual/XRNetworkPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!prefab.GetComponent<NetworkHandJointSync>()) prefab.AddComponent<NetworkHandJointSync>();
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            var feature = settings.GetFeature<SimultaneousHandsFeature>();
            if (!feature) throw new System.InvalidOperationException("Simultaneous hands feature not discovered");
            feature.enabled = true; EditorUtility.SetDirty(feature); EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Detailed hand joint views, network behaviour and optional simultaneous tracking enabled.");
        }
    }
}
