// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Helpers for using the Unity Package Manager.

using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
    static class PackageManagerHelpers
    {
        static AddRequest s_Request;
        public static bool IsBusy => s_Request?.IsCompleted == false;

        public static void AddMissingPackage(string packageName)
        {
            s_Request = Client.Add(packageName);
            EditorApplication.update += AddMissingPackageProgress;
        }

        static void AddMissingPackageProgress()
        {
            if (s_Request.IsCompleted)
            {
                if (s_Request.Status == StatusCode.Success)
                {
                    Debug.Log("Installed: " + s_Request.Result.packageId);
                }
                else if (s_Request.Status >= StatusCode.Failure)
                {
                    Debug.Log(s_Request.Error.message);
                }

                EditorApplication.update -= AddMissingPackageProgress;
            }
        }
    }
}
