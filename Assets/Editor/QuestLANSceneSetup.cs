using System;
using System.IO;
using QuestLAN.Avatar;
using QuestLAN.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;

namespace QuestLAN.Editor
{
    public static class QuestLANSceneSetup
    {
        [MenuItem("QuestLAN/Create initial MainScene (once)")]
        public static void CreateMainScene()
        {
            const string path = "Assets/Scenes/MainScene.unity";
            if (File.Exists(path)) throw new InvalidOperationException("MainScene already exists; edit it explicitly.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rig = new GameObject("Local XR Rig");
            var offset = new GameObject("Camera Floor Offset");
            offset.transform.SetParent(rig.transform, false);
            var cameraObject = new GameObject("XR Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var driver = cameraObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("Head Position", InputActionType.Value,
                "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("Head Tracking State", InputActionType.Value,
                "<XRHMD>/trackingState", expectedControlType: "Integer"));
            driver.ignoreTrackingState = false;
            var origin = rig.AddComponent<XROrigin>();
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            var root = new GameObject("Local AvatarRoot").transform;
            var head = Visual("Head (hidden locally)", PrimitiveType.Sphere, root, new Vector3(0.18f, 0.18f, 0.18f));
            var left = Visual("Left Wrist", PrimitiveType.Cube, root, new Vector3(0.08f, 0.04f, 0.14f));
            var right = Visual("Right Wrist", PrimitiveType.Sphere, root, new Vector3(0.08f, 0.08f, 0.08f));
            var source = rig.AddComponent<XRHandsPoseSource>();
            source.Configure(offset.transform, cameraObject.transform, root);
            var view = root.gameObject.AddComponent<AvatarPoseView>();
            view.Configure(source, root, head, left, right);
            head.gameObject.SetActive(false);
            left.gameObject.SetActive(false);
            right.gameObject.SetActive(false);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Shared Floor";
            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            Debug.Log("QuestLAN MainScene generated with Unity APIs. Configure OpenXR before hardware testing.");
        }

        private static Transform Visual(string name, PrimitiveType type, Transform parent, Vector3 scale)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            return visual.transform;
        }

        public static void PrepareM1Batch()
        {
            foreach (var sample in Sample.FindByPackage("com.unity.xr.hands", "1.7.3"))
            {
                if (sample.displayName != "HandVisualizer") continue;
                if (!sample.isImported && !sample.Import())
                    throw new InvalidOperationException("Official HandVisualizer import failed.");
                Debug.Log("QuestLAN official HandVisualizer imported: " + sample.importPath);
            }
            CreateMainScene();
            AssetDatabase.SaveAssets();
        }
    }
}
