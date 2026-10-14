using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace QuestLAN.Editor
{
    /// <summary>Run in a fresh batch Editor; package installation is performed by UPM.</summary>
    public static class QuestLANBootstrap
    {
        private static AddRequest request;
        private static int nextPackage;
        private static double started;
        private static readonly string[] Packages =
        {
            "com.unity.xr.management@4.5.4",
            "com.unity.xr.openxr@1.16.1",
            "com.unity.xr.hands@1.7.3",
            "com.unity.xr.interaction.toolkit@3.3.1"
        };

        public static void InstallXRBatch()
        {
            if (Application.unityVersion != "6000.3.15f1")
                throw new InvalidOperationException("QuestLAN requires Unity 6000.3.15f1.");
            started = EditorApplication.timeSinceStartup;
            // Prevent a reload between sequential UPM requests. A fresh Editor compiles afterwards.
            EditorApplication.LockReloadAssemblies();
            EditorApplication.update += Poll;
            request = Client.Add(Packages[nextPackage++]);
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup - started > 600)
            {
                Finish(1, "QuestLAN UPM timeout.");
                return;
            }
            if (!request.IsCompleted) return;
            if (request.Status == StatusCode.Failure)
            {
                Finish(1, request.Error.message);
                return;
            }
            Debug.Log("QuestLAN installed " + request.Result.name + "@" + request.Result.version);
            if (nextPackage < Packages.Length)
                request = Client.Add(Packages[nextPackage++]);
            else
                Finish(0, "QuestLAN UPM installation completed; reopen to compile and validate.");
        }

        private static void Finish(int exitCode, string message)
        {
            EditorApplication.update -= Poll;
            Debug.Log(message);
            EditorApplication.UnlockReloadAssemblies();
            EditorApplication.Exit(exitCode);
        }
    }
}
