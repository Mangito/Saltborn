// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityInputSystem && ENABLE_INPUT_SYSTEM
#define INPUT_SYSTEM_ENABLED
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    [@ExecuteDuringEditMode]
    [AddComponentMenu(Constants.k_MenuPrefixDebug + "Debug GUI")]
    sealed class DebugGUI : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        bool _ShowWaterData = true;

        [SerializeField]
        bool _GuiVisible = true;

        [SerializeField]
        bool _DrawLodDatasActualSize = false;

        [UnityEngine.Range(0f, 1f)]
        [SerializeField]
        float _PausedScroll;


        [Header("Simulations")]

        [SerializeField]
        bool _DrawAnimatedWaves = true;

        [SerializeField]
        bool _DrawDynamicWaves = false;

        [SerializeField]
        bool _DrawFoam = false;

        [SerializeField]
        bool _DrawFlow = false;

        [SerializeField]
        bool _DrawShadow = false;

        [SerializeField]
        bool _DrawDepth = false;

        [SerializeField]
        bool _DrawClip = false;


        const float k_ScrollBarWidth = 20f;
        float _Scroll;

        static readonly float s_LeftPanelWidth = 180f;
        static readonly float s_BottomPanelHeight = 25f;
        static readonly Color s_GuiColor = Color.black * 0.7f;

        WaterRenderer _Water;

        static class ShaderIDs
        {
            public static readonly int s_Depth = Shader.PropertyToID("_Depth");
            public static readonly int s_Scale = Shader.PropertyToID("_Scale");
            public static readonly int s_Bias = Shader.PropertyToID("_Bias");
        }

        static readonly Dictionary<System.Type, string> s_SimulationNames = new();

        static Material s_DebugArrayMaterial;
        static Material DebugArrayMaterial
        {
            get
            {
                if (s_DebugArrayMaterial == null) s_DebugArrayMaterial = new(WaterResources.Instance.Shaders._DebugTextureArray);
                return s_DebugArrayMaterial;
            }
        }

        static DebugGUI s_Instance;

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;

        public static bool OverGUI(Vector2 screenPosition)
        {
            if (s_Instance == null)
            {
                return false;
            }

            // Over left panel.
            if (s_Instance._GuiVisible && screenPosition.x < s_LeftPanelWidth)
            {
                return true;
            }

            // Over bottom panel.
            if (s_Instance._ShowWaterData && screenPosition.y < s_BottomPanelHeight)
            {
                return true;
            }

            // Over scroll bar.
            if (s_Instance._ShowWaterData && screenPosition.x > Screen.width - k_ScrollBarWidth)
            {
                return true;
            }

            return false;
        }

        private protected override void Initialize()
        {
            base.Initialize();
            s_Instance = this;
        }

        private protected override void OnDisable()
        {
            base.OnDisable();
            s_Instance = null;
        }

        void OnDestroy()
        {
            // Safe as there should only be one instance at a time.
            Helpers.Destroy(s_DebugArrayMaterial);
        }

        Vector3 _ViewerPositionLastFrame;
        Vector3 _ViewerVelocity;

        void OnUpdate(WaterRenderer water)
        {
            _Water = water;

            if (_Water.Viewpoint != null)
            {
                _ViewerVelocity = (_Water.Viewpoint.position - _ViewerPositionLastFrame) / Time.deltaTime;
                _ViewerPositionLastFrame = _Water != null ? _Water.Viewpoint.position : Vector3.zero;
            }

            // New input system works even when game view is not focused.
            if (!Application.isFocused)
            {
                return;
            }

#if INPUT_SYSTEM_ENABLED
            if (Keyboard.current.gKey.wasPressedThisFrame)
#else
            if (Input.GetKeyDown(KeyCode.G))
#endif
            {
                ToggleGUI();
            }
#if INPUT_SYSTEM_ENABLED
            if (Keyboard.current.fKey.wasPressedThisFrame)
#else
            if (Input.GetKeyDown(KeyCode.F))
#endif
            {
                Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
            }
#if INPUT_SYSTEM_ENABLED
            if (Keyboard.current.rKey.wasPressedThisFrame)
#else
            if (Input.GetKeyDown(KeyCode.R))
#endif
            {
                SceneManager.LoadScene(SceneManager.GetSceneAt(0).buildIndex);
            }
        }

        void OnGUI()
        {
            _Water = WaterRenderer.Instance;

            var bkp = GUI.color;

            if (_GuiVisible)
            {
                GUI.skin.toggle.normal.textColor = Color.white;
                GUI.skin.label.normal.textColor = Color.white;

                float x = 5f, y = 0f;
                float w = s_LeftPanelWidth - 2f * x, h = 25f;

                GUI.color = s_GuiColor;
                GUI.DrawTexture(new(0, 0, w + 2f * x, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.changed = false;
                var freeze = GUI.Toggle(new(x, y, w, h), Time.timeScale == 0f, "Freeze time (F)"); y += h;
                if (GUI.changed)
                {
                    Time.timeScale = freeze ? 0f : 1f;
                }

                // Time scale
                if (_Water)
                {
                    GUI.Label(new(x, y, w, h), $"Time Scale: {Time.timeScale}"); y += h;
                    Time.timeScale = GUI.HorizontalSlider(new(x, y, w, h), Time.timeScale, 1f, 30f); y += h;
                }

                // Global wind speed
                if (_Water)
                {
                    GUI.Label(new(x, y, w, h), "Global Wind Speed"); y += h;
                    _Water._WindSpeed = GUI.HorizontalSlider(new(x, y, w, h), _Water._WindSpeed, 0f, 150f); y += h;
                }

                OnGUIGerstnerSection(x, ref y, w, h);

                _ShowWaterData = GUI.Toggle(new(x, y, w, h), _ShowWaterData, "Show sim data"); y += h;

                AnimatedWavesLod.s_Combine = GUI.Toggle(new(x, y, w, h), AnimatedWavesLod.s_Combine, "Shape combine pass"); y += h;

                ShadowLod.s_ProcessData = GUI.Toggle(new(x, y, w, h), ShadowLod.s_ProcessData, "Process Shadows"); y += h;

                if (_Water)
                {
                    if (_Water._DynamicWavesLod.Enabled)
                    {
                        var dt = 1f / _Water._DynamicWavesLod.SimulationFrequency;
                        var steps = _Water._DynamicWavesLod.LastUpdateSubstepCount;
                        GUI.Label(new(x, y, w, h), string.Format("Sim steps: {0:0.00000} x {1}", dt, steps)); y += h;
                    }

                    if (_Water.AnimatedWavesLod.Provider is IQueryable querySystem)
                    {
                        GUI.Label(new(x, y, w, h), $"Query result GUIDs: {querySystem.ResultGuidCount}"); y += h;
                        GUI.Label(new(x, y, w, h), $"Queries in flight: {querySystem.RequestCount}"); y += h;
                        GUI.Label(new(x, y, w, h), $"Query Count: {querySystem.QueryCount}"); y += h;
                    }

#if UNITY_EDITOR
                    if (GUI.Button(new(x, y, w, h), "Select Water Material"))
                    {
                        var path = UnityEditor.AssetDatabase.GetAssetPath(_Water.Material);
                        var asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
                        UnityEditor.Selection.activeObject = asset;
                    }
                    y += h;
#endif
                }

                if (GUI.Button(new(x, y, w, h), "Hide GUI (G)"))
                {
                    ToggleGUI();
                }
                y += h;
            }

            // draw source textures to screen
            if (_ShowWaterData && _Water != null)
            {
                DrawShapeTargets();
            }

            GUI.color = bkp;
        }

        void OnGUIGerstnerSection(float x, ref float y, float w, float h)
        {
            GUI.Label(new(x, y, w, h), "Gerstner weight(s)"); y += h;

            foreach (var gerstner in ShapeGerstner.s_Instances)
            {
                var specW = 75f;
                gerstner.Value.Weight = GUI.HorizontalSlider(new(x, y, w - specW - 5f, h), gerstner.Value.Weight, 0f, 1f);

#if UNITY_EDITOR
                if (GUI.Button(new(x + w - specW, y, specW, h), "Spectrum"))
                {
                    var path = UnityEditor.AssetDatabase.GetAssetPath(gerstner.Value._Spectrum);
                    var asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
                    UnityEditor.Selection.activeObject = asset;
                }
#endif
                y += h;
            }

            GUI.Label(new(x, y, w, h), $"FFT generator(s): {FFTCompute.GeneratorCount}"); y += h;
        }

        void DrawShapeTargets()
        {
            // Draw bottom panel for toggles
            var bottomBar = new Rect(_GuiVisible ? s_LeftPanelWidth : 0,
                Screen.height - s_BottomPanelHeight, Screen.width, s_BottomPanelHeight);
            GUI.color = s_GuiColor;
            GUI.DrawTexture(bottomBar, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Show viewer height above water in bottom panel
            bottomBar.x += 10;
            GUI.Label(bottomBar, "Viewer Height Above Water: " + _Water.ViewerHeightAboveWater);

            // Viewer speed
            {
                bottomBar.x += 250;
                GUI.Label(bottomBar, "Speed: " + (3.6f * _ViewerVelocity.magnitude) + "km/h");
            }

            // Draw sim data
            DrawSims();
        }

        void DrawSims()
        {
            var column = 1f;

            DrawVerticalScrollBar();

            DrawSim(_Water._AnimatedWavesLod, ref _DrawAnimatedWaves, ref column, 0.5f);
            DrawSim(_Water._DynamicWavesLod, ref _DrawDynamicWaves, ref column, 0.5f, 2f);
            DrawSim(_Water._FoamLod, ref _DrawFoam, ref column);
            DrawSim(_Water._FlowLod, ref _DrawFlow, ref column, 0.5f, 2f);
            DrawSim(_Water._ShadowLod, ref _DrawShadow, ref column);
            DrawSim(_Water._DepthLod, ref _DrawDepth, ref column);
            DrawSim(_Water._ClipLod, ref _DrawClip, ref column);
        }

        void DrawVerticalScrollBar()
        {
            if (!_DrawLodDatasActualSize)
            {
                return;
            }

            // Data is uniform so use animated waves since it should always be there.
            var lodData = _Water._AnimatedWavesLod;

            // Make scroll bar wider as resizable window hover area covers part of it.
            var style = GUI.skin.verticalScrollbar;
            style.fixedWidth = k_ScrollBarWidth;

            var height = Screen.height - s_BottomPanelHeight;
            var rect = new Rect(Screen.width - style.fixedWidth, 0f, style.fixedWidth, height);

            // Background.
            GUI.color = s_GuiColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var scrollSize = lodData.DataTexture.height * lodData.DataTexture.volumeDepth - height;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
            {
                _Scroll = _PausedScroll * scrollSize;
            }
#endif

            _Scroll = GUI.VerticalScrollbar
            (
                rect,
                _Scroll,
                size: height,
                topValue: 0f,
                bottomValue: lodData.DataTexture.height * lodData.DataTexture.volumeDepth,
                style
            );

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPaused)
            {
                _PausedScroll = Mathf.Clamp01(_Scroll / scrollSize);
            }
#endif
        }

        void DrawSim(Lod lodData, ref bool doDraw, ref float offset, float bias = 0f, float scale = 1f)
        {
            if (lodData == null) return;
            if (!lodData.Enabled) return;

            // Compute short names that will fit in UI and cache them.
            var type = lodData.GetType();
            if (!s_SimulationNames.ContainsKey(type))
            {
                s_SimulationNames.Add(type, lodData.ID);
            }

            var isRightmost = offset == 1f;

            // Zero out here so we maintain scroll when switching back to actual size.
            var scroll = _DrawLodDatasActualSize ? _Scroll : 0f;

            var togglesBegin = Screen.height - s_BottomPanelHeight;
            var b = 7f;
            var h = _DrawLodDatasActualSize ? lodData.DataTexture.height : togglesBegin / lodData.DataTexture.volumeDepth;
            var w = h + b;
            var x = Screen.width - w * offset + b * (offset - 1f);
            if (_DrawLodDatasActualSize) x -= k_ScrollBarWidth;

            if (doDraw)
            {
                // Background behind slices
                GUI.color = s_GuiColor;
                GUI.DrawTexture(new(x, 0, isRightmost ? w : w - b, Screen.height - s_BottomPanelHeight), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Only use Graphics.DrawTexture in EventType.Repaint events if called in OnGUI
                if (Event.current.type.Equals(EventType.Repaint))
                {
                    for (var idx = 0; idx < lodData.DataTexture.volumeDepth; idx++)
                    {
                        var y = idx * h;
                        if (isRightmost) w += b;

                        // Render specific slice of 2D texture array
                        DebugArrayMaterial.SetInteger(ShaderIDs.s_Depth, idx);
                        DebugArrayMaterial.SetFloat(ShaderIDs.s_Scale, scale);
                        DebugArrayMaterial.SetFloat(ShaderIDs.s_Bias, bias);
                        Graphics.DrawTexture(new(x + b, (y + b / 2f) - scroll, h - b, h - b), lodData.DataTexture, DebugArrayMaterial);
                    }
                }
            }

            doDraw = GUI.Toggle(new(x + b, togglesBegin, w - 2f * b, s_BottomPanelHeight), doDraw, s_SimulationNames[type]);

            offset++;
        }

        public static void DrawTextureArray(RenderTexture data, int columnOffsetFromRightSide, float bias = 0f, float scale = 1f)
        {
            var offset = columnOffsetFromRightSide;

            var togglesBegin = Screen.height - s_BottomPanelHeight;
            var b = 1f;
            var h = togglesBegin / data.volumeDepth;
            var w = h + b;
            var x = Screen.width - w * offset + b * (offset - 1f);

            {
                // Background behind slices
                GUI.color = s_GuiColor;
                GUI.DrawTexture(new(x, 0, offset == 1f ? w : w - b, Screen.height - s_BottomPanelHeight), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Only use Graphics.DrawTexture in EventType.Repaint events if called in OnGUI
                if (Event.current.type.Equals(EventType.Repaint))
                {
                    for (var idx = 0; idx < data.volumeDepth; idx++)
                    {
                        var y = idx * h;
                        if (offset == 1f) w += b;

                        // Render specific slice of 2D texture array
                        DebugArrayMaterial.SetInteger(ShaderIDs.s_Depth, idx);
                        DebugArrayMaterial.SetFloat(ShaderIDs.s_Scale, scale);
                        DebugArrayMaterial.SetFloat(ShaderIDs.s_Bias, bias);
                        Graphics.DrawTexture(new(x + b, y + b / 2f, h - b, h - b), data, DebugArrayMaterial);
                    }
                }
            }
        }

        void ToggleGUI()
        {
            _GuiVisible = !_GuiVisible;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStatics()
        {
            // Init here from 2019.3 onwards
            s_SimulationNames.Clear();
        }
    }
}
