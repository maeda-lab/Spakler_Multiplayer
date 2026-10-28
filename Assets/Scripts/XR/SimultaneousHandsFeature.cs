using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor.XR.OpenXR.Features;
#endif

namespace QuestLAN.XR
{
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "QuestLAN simultaneous hands and controllers", Company = "QuestLAN",
        BuildTargetGroups = new[] { UnityEditor.BuildTargetGroup.Standalone },
        Desc = "Optional runtime hand/controller multimodal tracking; unavailable joints remain invalid.",
        OpenxrExtensionStrings = Extension, Version = "1.0.0", FeatureId = "questlan.openxr.simultaneous-hands")]
#endif
    public sealed class SimultaneousHandsFeature : OpenXRFeature
    {
        public const string Extension = "XR_META_simultaneous_hands_and_controllers";
        public static string Status { get; private set; } = "Not started";
        [StructLayout(LayoutKind.Sequential)] private struct ResumeInfo { public int Type; public IntPtr Next; }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetProc(ulong instance, [MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Resume(ulong session, ref ResumeInfo info);
        private Resume resume;
        protected override bool OnInstanceCreate(ulong instance)
        {
            Status = "Unavailable";
            if (!OpenXRRuntime.IsExtensionEnabled(Extension)) return false;
            var getProc = Marshal.GetDelegateForFunctionPointer<GetProc>(xrGetInstanceProcAddr);
            if (getProc(instance, "xrResumeSimultaneousHandsAndControllersTrackingMETA", out var address) < 0 || address == IntPtr.Zero) return false;
            resume = Marshal.GetDelegateForFunctionPointer<Resume>(address);
            Status = "Available; waiting for session";
            return true;
        }
        protected override void OnSessionBegin(ulong session)
        {
            if (resume == null) return;
            // XrStructureType value from Khronos OpenXR header; Windows x64 sequential struct.
            var info = new ResumeInfo { Type = 1000532002, Next = IntPtr.Zero };
            int result = resume(session, ref info);
            Status = result == 0 ? "Active" : "Unavailable (result " + result + ")";
            Debug.Log("[Hands] Simultaneous tracking: " + Status);
        }
        protected override void OnSessionDestroy(ulong session) { Status = "Session stopped"; }
        protected override void OnInstanceDestroy(ulong instance) { resume = null; Status = "Not started"; }
    }
}
