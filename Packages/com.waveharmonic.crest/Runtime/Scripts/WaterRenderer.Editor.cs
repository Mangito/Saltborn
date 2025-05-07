// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest
{
    [@ExecuteDuringEditMode(ExecuteDuringEditMode.Include.None, ExecuteDuringEditMode.Options.Singleton)]
    [SelectionBase]
    [AddComponentMenu(Constants.k_MenuPrefixScripts + "Water Renderer")]
    [@HelpURL("About/Introduction.html")]
    sealed partial class WaterRenderer
    {
        internal const string k_ProxyShader = "Hidden/Crest/Editor/WaterProxy";
        internal GameObject _ProxyPlane;
        internal bool IsProxyPlaneRendering => !Application.isPlaying && _ShowWaterProxyPlane;

        internal static float LastUpdateEditorTime { get; set; } = -1f;
        static int s_EditorFrames = 0;

        // Useful for rate limiting processes called outside of RunUpdate like camera events.
        static int s_EditorFramesSinceUpdate = 0;
        static int EditorFramesSinceUpdate => Application.isPlaying ? 0 : s_EditorFramesSinceUpdate;
        internal static bool IsWithinEditorUpdate => EditorFramesSinceUpdate == 0;

        internal bool IsSceneViewActive { get; set; }

        int _LastFrameSceneCamera;

        private protected override void Reset()
        {
            _Underwater._Material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.waveharmonic.crest/Runtime/Materials/Water Volume.mat");

            base.Reset();
        }

        // Tracks the scene view rendering to determine if a scene camera is active.
        void UpdateLastActiveSceneCamera(Camera camera)
        {
            if (camera.cameraType == CameraType.SceneView)
            {
                IsSceneViewActive = true;
                _LastFrameSceneCamera = Time.frameCount;
            }

            if (_LastFrameSceneCamera < Time.frameCount - 1)
            {
                IsSceneViewActive = false;
            }
        }

        static void EditorUpdate()
        {
            // Do not execute when editor is not active to conserve power and prevent possible leaks.
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                return;
            }

            s_EditorFramesSinceUpdate++;

            if (Instance == null) return;

            if (!Application.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - LastUpdateEditorTime > 1f / Mathf.Clamp(Instance._EditModeFrameRate, 0.01f, 60f))
                {
                    s_EditorFrames++;
                    s_EditorFramesSinceUpdate = 0;

                    LastUpdateEditorTime = (float)EditorApplication.timeSinceStartup;

                    Instance.RunUpdate();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnDomainReload()
        {
            s_EditorFramesSinceUpdate = 0;
            AfterRuntimeLoad();
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnReLoadScripts()
        {
            AfterScriptReload();
        }
    }

    partial class WaterRenderer
    {
        /// <inheritdoc/>
        private protected override void OnValidate()
        {
            base.OnValidate();

            // Must be at least 0.25, and must be on a power of 2
            _ScaleRange.x = Mathf.Pow(2f, Mathf.Round(Mathf.Log(Mathf.Max(_ScaleRange.x, 0.25f), 2f)));

            if (_ScaleRange.y < Mathf.Infinity)
            {
                // otherwise must be at least 0.25, and must be on a power of 2
                _ScaleRange.y = Mathf.Pow(2f, Mathf.Round(Mathf.Log(Mathf.Max(_ScaleRange.y, _ScaleRange.x), 2f)));
            }

            // Gravity 0 makes waves freeze which is weird but doesn't seem to break anything so allowing this for now
            _GravityMultiplier = Mathf.Max(_GravityMultiplier, 0f);

            // LOD data resolution multiple of 2 for general GPU texture reasons (like pixel quads)
            _Resolution -= _Resolution % 2;

            _GeometryDownSampleFactor = Mathf.ClosestPowerOfTwo(Mathf.Max(_GeometryDownSampleFactor, 1));

            var remGeo = _Resolution % _GeometryDownSampleFactor;
            if (remGeo > 0)
            {
                var newLDR = _Resolution - (_Resolution % _GeometryDownSampleFactor);
                Debug.LogWarning
                (
                    $"Crest: Adjusted Lod Data Resolution from {_Resolution} to {newLDR} to ensure the Geometry Down Sample Factor is a factor ({_GeometryDownSampleFactor}).",
                    this
                );

                _Resolution = newLDR;
            }
        }
    }
}

#endif
