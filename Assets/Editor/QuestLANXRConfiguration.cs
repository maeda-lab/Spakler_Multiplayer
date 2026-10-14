using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Hands.Samples.VisualizerSample;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Unity.XR.CoreUtils;

namespace QuestLAN.Editor
{
    public static class QuestLANXRConfiguration
    {
        [MenuItem("QuestLAN/Configure OpenXR platforms")]
        public static void ConfigureBatch()
        {
            if (Application.unityVersion != "6000.3.15f1")
                throw new InvalidOperationException("Requires Unity 6000.3.15f1.");
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget targets))
            {
                const string path = "Assets/XR/Settings/QuestLAN XR General Settings.asset";
                targets = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (!targets)
                {
                    targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(targets, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, targets, true);
            }
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!targets.HasManagerSettingsForBuildTarget(group))
                    targets.CreateDefaultManagerSettingsForBuildTarget(group);
                var general = targets.SettingsForBuildTarget(group);
                general.InitManagerOnStart = true;
                if (general.Manager.activeLoaders.Any(loader => !(loader is OpenXRLoader)))
                    throw new InvalidOperationException("Competing XR loader found; resolve explicitly: " + group);
                if (!XRPackageMetadataStore.AssignLoader(general.Manager,
                    typeof(OpenXRLoader).FullName, group))
                    throw new InvalidOperationException("OpenXR loader assignment failed: " + group);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                Enable<HandTracking>(settings);
                Enable<OculusTouchControllerProfile>(settings);
                if (group == BuildTargetGroup.Android) Enable<MetaQuestFeature>(settings);
                settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                EditorUtility.SetDirty(general);
                EditorUtility.SetDirty(general.Manager);
                EditorUtility.SetDirty(settings);
            }
            EditorUtility.SetDirty(targets);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.maedalab.questlanprototype");
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            AssetDatabase.SaveAssets();
            AddOfficialVisualizer();
            ValidateBatch();
        }

        private static void Enable<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            if (!settings) throw new InvalidOperationException("Missing OpenXR settings.");
            var feature = settings.GetFeature<T>();
            if (!feature) throw new InvalidOperationException("Feature not registered: " + typeof(T).FullName);
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        private static void AddOfficialVisualizer()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new InvalidOperationException("Scene save cancelled.");
            var main = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            if (!origin || UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("MainScene requires exactly one XR Origin.");
            if (!UnityEngine.Object.FindFirstObjectByType<HandVisualizer>())
            {
                var sample = EditorSceneManager.OpenScene(
                    "Assets/Samples/XR Hands/1.7.3/HandVisualizer/HandVisualizer.unity", OpenSceneMode.Additive);
                var template = sample.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<HandVisualizer>(true)).Single();
                var visualObject = new GameObject("Official HandVisualizer");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visualObject, main);
                visualObject.transform.SetParent(origin.CameraFloorOffsetObject.transform, false);
                var visualizer = visualObject.AddComponent<HandVisualizer>();
                EditorUtility.CopySerialized(template, visualizer);
                visualizer.drawMeshes = true;
                visualizer.debugDrawJoints = false;
                // Verify copied references point at reusable assets, not sample scene objects.
                var serialized = new SerializedObject(visualizer);
                foreach (var field in new[] { "m_MetaQuestLeftHandMesh", "m_MetaQuestRightHandMesh",
                    "m_DebugDrawPrefab", "m_VelocityPrefab" })
                {
                    var reference = serialized.FindProperty(field).objectReferenceValue;
                    if (!reference || !EditorUtility.IsPersistent(reference))
                        throw new InvalidOperationException("Visualizer reference is not an asset: " + field);
                }
                EditorSceneManager.CloseScene(sample, true);
                EditorSceneManager.SaveScene(main);
            }
        }

        public static void ValidateBatch()
        {
            var report = new List<string> { "Unity " + Application.unityVersion,
                "Active build target: " + EditorUserBuildSettings.activeBuildTarget };
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (!general || !general.Manager || !general.InitManagerOnStart ||
                    !general.Manager.activeLoaders.Any(loader => loader is OpenXRLoader) ||
                    !settings.GetFeature<HandTracking>().enabled)
                    throw new InvalidOperationException("Required XR configuration missing: " + group);
                if (group != BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget))
                {
                    report.Add(group + ": settings checked; validation requires that platform to be active.");
                    continue;
                }
                var issues = new List<OpenXRFeature.ValidationRule>();
                OpenXRProjectValidation.GetCurrentValidationIssues(issues, group);
                report.Add(group + ": " + issues.Count(issue => issue.error) + " errors, " +
                    issues.Count(issue => !issue.error) + " warnings");
                foreach (var issue in issues)
                    report.Add((issue.error ? "ERROR: " : "WARNING: ") + issue.message +
                        " | Fix: " + issue.fixItMessage);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/QuestLAN-XRValidation-" + EditorUserBuildSettings.activeBuildTarget + ".txt", report);
            Debug.Log(string.Join("\n", report));
        }

        public static void BuildAndroidBatch()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Launch Editor with -buildTarget Android.");
            ValidateBatch();
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            if (issues.Any(issue => issue.error))
                throw new InvalidOperationException("Resolve reported Android OpenXR errors before building.");
            Directory.CreateDirectory("Builds/Android");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainScene.unity" },
                locationPathName = "Builds/Android/QuestLANPrototype.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            Debug.Log("QuestLAN Android build: " + result.summary.result + "; errors=" +
                result.summary.totalErrors + "; bytes=" + result.summary.totalSize);
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed. Inspect the build log.");
        }
    }
}
