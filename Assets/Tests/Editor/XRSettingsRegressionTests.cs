using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuestLAN.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace QuestLAN.EditorTests
{
    public sealed class XRSettingsRegressionTests
    {
        [Test]
        public void SettingsContainNoUnresolvedMergeText()
        {
            foreach (var path in new[] { "ProjectSettings/ProjectSettings.asset",
                "ProjectSettings/EditorBuildSettings.asset", "Assets/XR/Settings/OpenXR Editor Settings.asset.meta" })
                Assert.That(Regex.IsMatch(File.ReadAllText(path),
                    @"(?m)^\s*(?:<{7}|={7}|>{7}|feature/network\s*$)"), Is.False, path);
        }

        [Test]
        public void PlayerSettingsDeserializeAndEnableInputSystem()
        {
            var text = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            var product = Regex.Match(text, @"(?m)^  productName: (.+)\r?$");
            Assert.That(product.Success, Is.True);
            Assert.That(PlayerSettings.productName, Is.EqualTo(product.Groups[1].Value.Trim()));
            Assert.That(Regex.IsMatch(text, @"(?m)^  activeInputHandler: [12]\s*$"), Is.True);
        }

        [Test]
        public void RegisteredSettingsUseConfiguredAssetAndBothPlatformsHaveOpenXRHands()
        {
            Assert.That(EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget registered), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(registered),
                Is.EqualTo("Assets/XR/Settings/QuestLAN XR General Settings.asset"));
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var general = registered.SettingsForBuildTarget(group);
                Assert.That(general, Is.Not.Null, group.ToString());
                Assert.That(general.InitManagerOnStart, Is.EqualTo(group == BuildTargetGroup.Android), "Windows must defer XR until room entry; Android is retained as history.");
                Assert.That(general.Manager, Is.Not.Null);
                Assert.That(general.Manager.activeLoaders.Count, Is.EqualTo(1));
                Assert.That(general.Manager.activeLoaders[0], Is.InstanceOf<OpenXRLoader>());
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                Assert.That(settings.GetFeature<HandTracking>().enabled, Is.True);
                if (group == BuildTargetGroup.Android)
                    Assert.That(settings.GetFeature<MetaQuestFeature>().enabled, Is.True);
            }
        }

        [Test]
        public void MainSceneLoadsOneTrackedRigWithFloorAndNoMissingScripts()
        {
            const string path = "Assets/Scenes/MainScene.unity";
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var origins = objects.Select(item => item.GetComponent<XROrigin>()).Where(item => item).ToArray();
                Assert.That(origins.Length, Is.EqualTo(1));
                Assert.That(origins[0].Camera, Is.Not.Null);
                Assert.That(origins[0].Camera.enabled, Is.True);
                Assert.That(origins[0].Camera.GetComponent<TrackedPoseDriver>(), Is.Not.Null);
                Assert.That(origins[0].CameraFloorOffsetObject, Is.Not.Null);
                Assert.That(objects.Count(item => item.GetComponent<XRHandsPoseSource>()), Is.EqualTo(1));
                Assert.That(objects.Any(item => item.name == "Shared Floor" && item.GetComponent<Renderer>()), Is.True);
                foreach (var item in objects)
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject), Is.Zero, item.name);
            }
            finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
