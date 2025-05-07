// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Editor
{
    [CustomEditor(typeof(WaterRenderer))]
    sealed class WaterRendererEditor : Inspector
    {
        WaterRenderer _Target;

        void OnEnable()
        {
            _Target = (WaterRenderer)target;
        }

        protected override void RenderInspectorGUI()
        {
            var target = this.target as WaterRenderer;

            var currentAssignedTP = serializedObject.FindProperty(nameof(WaterRenderer._TimeProvider)).objectReferenceValue;

            base.RenderInspectorGUI();

            // Detect if user changed TP, if so update stack
            var newlyAssignedTP = serializedObject.FindProperty(nameof(WaterRenderer._TimeProvider)).objectReferenceValue;
            if (currentAssignedTP != newlyAssignedTP)
            {
                if (currentAssignedTP != null)
                {
                    target.TimeProviders.Pop(currentAssignedTP as TimeProvider);
                }
                if (newlyAssignedTP != null)
                {
                    target.TimeProviders.Push(newlyAssignedTP as TimeProvider);
                }
            }

            // Display version in information box.
            {
                // Fix leftover nesting from drawers.
                EditorGUI.indentLevel = 0;

                var padding = GUI.skin.GetStyle("HelpBox").padding;
                GUI.skin.GetStyle("HelpBox").padding = new(10, 10, 10, 10);

#if CREST_DEBUG
                if (target._Debug._ShowDebugInformation)
                {
                    EditorGUILayout.Space();

                    var baseScale = target.CalcLodScale(0);
                    var baseTexelSize = 2f * 2f * baseScale / target.LodResolution;

                    var message = "";
                    for (var i = 0; i < target.LodLevels; i++)
                    {
                        message += $"LOD: {i}\n";
                        message += $"Scale: {target.CalcLodScale(i)}\n";
                        message += $"Texel: {2f * 2f * target.CalcLodScale(i) / target.LodResolution}\n";
                        message += $"Minimum Slice: {Mathf.Floor(Mathf.Log(Mathf.Max(i / baseTexelSize, 1f), 2f))}";
                        if (i < target.LodLevels - 1) message += "\n\n";
                    }

                    if (target.Material.HasVector(WaterRenderer.ShaderIDs.s_Absorption))
                    {
                        message += $"\n\nDepth Fog Density: {target.Material.GetVector(WaterRenderer.ShaderIDs.s_Absorption)}";
                    }

                    EditorGUILayout.HelpBox(message, MessageType.None);
                }
#endif

                GUI.skin.GetStyle("HelpBox").padding = padding;
            }
        }

        protected override void RenderBottomButtons()
        {
            base.RenderBottomButtons();

            var target = this.target as WaterRenderer;

            EditorGUILayout.Space();

#if CREST_DEBUG
            if (GUILayout.Button("Change Scale"))
            {
                var scale = target.ScaleRange;
                scale.x = scale.x == 4f ? 256f : 4f;
                target.ScaleRange = scale;
                EditorApplication.isPaused = false;
            }
#endif

            if (GUILayout.Button("Validate Setup"))
            {
                ValidatedHelper.ExecuteValidators(target, ValidatedHelper.DebugLog);

                foreach (var component in FindObjectsByType<EditorBehaviour>(FindObjectsSortMode.None))
                {
                    if (component is WaterRenderer) continue;
                    ValidatedHelper.ExecuteValidators(component, ValidatedHelper.DebugLog);
                }

                Debug.Log("Crest: Validation complete!", target);
            }
        }
    }

    [CustomEditor(typeof(WaveSpectrum))]
    sealed class WaveSpectrumEditor : Inspector, IEmbeddableEditor
    {
        static readonly string[] s_ModelDescriptions = new[]
        {
            "Select an option to author waves using a spectrum model.",
            "Fully developed sea with infinite fetch.",
        };

        static readonly GUIContent s_TimeScaleLabel = new("Time Scale");

        System.Type _HostComponentType = null;
        public void SetTypeOfHostComponent(System.Type hostComponentType)
        {
            _HostComponentType = hostComponentType;
        }

        void OnEnable()
        {
            Undo.undoRedoEvent -= OnUndoRedo;
            Undo.undoRedoEvent += OnUndoRedo;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Undo.undoRedoEvent -= OnUndoRedo;
        }

        void OnUndoRedo(in UndoRedoInfo info)
        {
            var target = (WaveSpectrum)this.target;

            if (info.undoName == "Change Spectrum")
            {
                target.InitializeHandControls();
            }
        }

        protected override void RenderInspectorGUI()
        {
            // Display a notice if its being edited as a standalone asset (not embedded in a component) because
            // it displays the FFT-interface.
            if (_HostComponentType == null)
            {
                EditorGUILayout.HelpBox("This editor is displaying the FFT spectrum settings. " +
                    "To edit settings specific to the ShapeGerstner component, assign this asset to a ShapeGerstner component " +
                    "and edit it there by expanding the Spectrum field.", MessageType.Info);
                EditorGUILayout.Space();
            }

            base.RenderInspectorGUI();

            EditorGUI.BeginChangeCheck();

            var beingEditedOnGerstnerComponent = _HostComponentType == typeof(ShapeGerstner);

            var showAdvancedControls = false;
            if (beingEditedOnGerstnerComponent)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(WaveSpectrum._GravityScale)));

                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(WaveSpectrum._WaveDirectionVariance)));

                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(WaveSpectrum._ShowAdvancedControls)));
                showAdvancedControls = serializedObject.FindProperty(nameof(WaveSpectrum._ShowAdvancedControls)).boolValue;
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(WaveSpectrum._GravityScale)), s_TimeScaleLabel);
            }

            var spSpectrumModel = serializedObject.FindProperty(nameof(WaveSpectrum._Model));
            var spectraIndex = serializedObject.FindProperty(nameof(WaveSpectrum._Model)).enumValueIndex;
            var spectrumModel = (WaveSpectrum.SpectrumModel)Mathf.Clamp(spectraIndex, 0, 1);

            EditorGUILayout.Space();

            var spDisabled = serializedObject.FindProperty(nameof(WaveSpectrum._PowerDisabled));
            EditorGUILayout.BeginHorizontal();
            var allEnabled = true;
            for (var i = 0; i < spDisabled.arraySize; i++)
            {
                if (spDisabled.GetArrayElementAtIndex(i).boolValue) allEnabled = false;
            }
            var toggle = allEnabled;
            if (toggle != EditorGUILayout.Toggle(toggle, GUILayout.Width(13f)))
            {
                for (var i = 0; i < spDisabled.arraySize; i++)
                {
                    spDisabled.GetArrayElementAtIndex(i).boolValue = toggle;
                }
            }
            EditorGUILayout.LabelField("Spectrum", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            var spec = target as WaveSpectrum;

            var spPower = serializedObject.FindProperty(nameof(WaveSpectrum._PowerLogarithmicScales));
            var spChopScales = serializedObject.FindProperty(nameof(WaveSpectrum._ChopScales));
            var spGravScales = serializedObject.FindProperty(nameof(WaveSpectrum._GravityScales));

            // Disable sliders if authoring with model.
            var canEditSpectrum = spectrumModel == WaveSpectrum.SpectrumModel.None;

            for (var i = 0; i < spPower.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var spDisabled_i = spDisabled.GetArrayElementAtIndex(i);
                spDisabled_i.boolValue = !EditorGUILayout.Toggle(!spDisabled_i.boolValue, GUILayout.Width(15f));

                var smallWL = WaveSpectrum.SmallWavelength(i);
                var spPower_i = spPower.GetArrayElementAtIndex(i);

                var isPowerDisabled = spDisabled_i.boolValue;
                var powerValue = isPowerDisabled ? WaveSpectrum.s_MinimumPowerLog : spPower_i.floatValue;

                if (showAdvancedControls)
                {
                    EditorGUILayout.LabelField(string.Format("{0}", smallWL), EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    // Disable slider if authoring with model.
                    using (new EditorGUI.DisabledGroupScope(!canEditSpectrum || spDisabled_i.boolValue))
                    {
                        powerValue = EditorGUILayout.Slider("    Power", powerValue, WaveSpectrum.s_MinimumPowerLog, WaveSpectrum.s_MaximumPowerLog);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(string.Format("{0}", smallWL), GUILayout.Width(50f));

                    // Disable slider if authoring with model.
                    using (new EditorGUI.DisabledGroupScope(!canEditSpectrum || spDisabled_i.boolValue))
                    {
                        powerValue = EditorGUILayout.Slider(powerValue, WaveSpectrum.s_MinimumPowerLog, WaveSpectrum.s_MaximumPowerLog);
                    }

                    EditorGUILayout.EndHorizontal();
                    // This will create a tooltip for slider.
                    GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", powerValue.ToString()));
                }

                // If the power is disabled, we are using the MIN_POWER_LOG value so we don't want to store it.
                if (!isPowerDisabled)
                {
                    spPower_i.floatValue = powerValue;
                }

                if (showAdvancedControls)
                {
                    EditorGUILayout.Slider(spChopScales.GetArrayElementAtIndex(i), 0f, 4f, "    Chop Scale");
                    EditorGUILayout.Slider(spGravScales.GetArrayElementAtIndex(i), 0f, 4f, "    Grav Scale");
                }
            }


            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Empirical Spectra", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            spectrumModel = (WaveSpectrum.SpectrumModel)EditorGUILayout.EnumPopup(spectrumModel);
            spSpectrumModel.enumValueIndex = (int)spectrumModel;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(s_ModelDescriptions[(int)spectrumModel], MessageType.Info);
            EditorGUILayout.Space();

            if (spectrumModel == WaveSpectrum.SpectrumModel.None)
            {
                Undo.RecordObject(spec, "Change Spectrum");
            }
            else
            {
                // It doesn't seem to matter where this is called.
                Undo.RecordObject(spec, $"Apply {ObjectNames.NicifyVariableName(spectrumModel.ToString())} Spectrum");


                // Descriptions from this very useful paper:
                // https://hal.archives-ouvertes.fr/file/index/docid/307938/filename/frechot_realistic_simulation_of_ocean_surface_using_wave_spectra.pdf

                switch (spectrumModel)
                {
                    case WaveSpectrum.SpectrumModel.PiersonMoskowitz:
                        spec.ApplyPiersonMoskowitzSpectrum();
                        break;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                // NOTE: Undo/Redo will not update for some reason.
                serializedObject.ApplyModifiedProperties();
                spec.InitializeHandControls();
            }

            if (GUI.changed)
            {
                // We need to call this otherwise any property which has HideInInspector won't save.
                EditorUtility.SetDirty(spec);
            }
        }
    }

    [CustomEditor(typeof(LodSettings), true)]
    sealed class SimSettingsBaseEditor : Inspector
    {
        protected override void RenderInspectorGUI()
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Online Help Page"))
            {
                var targetType = target.GetType();
                var helpAttribute = (HelpURL)System.Attribute.GetCustomAttribute(targetType, typeof(HelpURL));
                Debug.AssertFormat(helpAttribute != null, "Crest: Could not get HelpURL attribute from {0}.", targetType);
                Application.OpenURL(helpAttribute.URL);
            }
            EditorGUILayout.Space();

            base.RenderInspectorGUI();
        }
    }

    [CustomEditor(typeof(WaterChunkRenderer)), CanEditMultipleObjects]
    sealed class WaterChunkRendererEditor : Inspector
    {
        Renderer _Renderer;
        protected override void RenderInspectorGUI()
        {
            base.RenderInspectorGUI();

            var target = this.target as WaterChunkRenderer;

            if (_Renderer == null)
            {
                _Renderer = target.GetComponent<Renderer>();
            }

            GUI.enabled = false;
            var boundsXZ = new Bounds(target._UnexpandedBoundsXZ.center.XNZ(), target._UnexpandedBoundsXZ.size.XNZ());
            EditorGUILayout.BoundsField("Bounds XZ", boundsXZ);
            EditorGUILayout.BoundsField("Expanded Bounds", _Renderer.bounds);
            GUI.enabled = true;
        }
    }

    [CustomEditor(typeof(DepthProbe))]
    sealed class DepthProbeEditor : Inspector
    {
        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            // Allows DepthProbe to trigger a bake without referencing assembly.
            DepthProbe.OnBakeRequest -= Bake;
            DepthProbe.OnBakeRequest += Bake;
        }

        protected override void RenderBottomButtons()
        {
            base.RenderBottomButtons();

            EditorGUILayout.Space();

            var target = this.target as DepthProbe;

            var isBaked = target.Type == DepthProbeMode.Baked;
            var onDemand = target.Type == DepthProbeMode.RealTime && target.RefreshMode == DepthProbeRefreshMode.ViaScripting;
            var canBake = !onDemand && !Application.isPlaying;
            var canPopulate = Application.isPlaying ? onDemand : target.Type != DepthProbeMode.Baked;

            if (target.SavedTexture != null && isBaked ? GUILayout.Button("Switch to Real-Time") : GUILayout.Button("Switch to Baked"))
            {
                Undo.RecordObject(target, isBaked ? "Switch to Real-Time" : "Switch to Baked");
                target.Type = isBaked ? DepthProbeMode.RealTime : DepthProbeMode.Baked;
                EditorUtility.SetDirty(target);
            }

            if (canPopulate && GUILayout.Button("Populate"))
            {
                target.Populate();
            }

            if (canBake && GUILayout.Button("Bake"))
            {
                Bake(target);
            }
        }

        internal static void Bake(DepthProbe target)
        {
            target.ForcePopulate();

            var rt = target.RealtimeTexture;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
            tex.ReadPixels(new(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;

            byte[] bytes;
            bytes = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

            var path = target.SavedTexture
                ? AssetDatabase.GetAssetPath(target.SavedTexture)
                : $"Assets/DepthProbe_{System.Guid.NewGuid()}.exr";
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);

            if (target.SavedTexture == null)
            {
                var serializedObject = new SerializedObject(target);
                serializedObject.FindProperty(nameof(DepthProbe._SavedTexture)).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                serializedObject.FindProperty(nameof(DepthProbe._Type)).enumValueIndex = (int)DepthProbeMode.Baked;
                serializedObject.ApplyModifiedProperties();
            }

            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            ti.textureShape = TextureImporterShape.Texture2D;
            ti.sRGBTexture = false;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = false;
            // Compression will clamp negative values.
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            // Values are slightly different with NPOT Scale applied.
            ti.npotScale = TextureImporterNPOTScale.None;

            // Set single component.
            if (!target._GenerateSignedDistanceField)
            {
                ti.textureType = TextureImporterType.SingleChannel;

                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                ti.SetTextureSettings(settings);
            }

            // Set format.
            {
                var settings = ti.GetDefaultPlatformTextureSettings();
                settings.format = target._GenerateSignedDistanceField ? TextureImporterFormat.RGFloat : TextureImporterFormat.RFloat;
                ti.SetPlatformTextureSettings(settings);
            }

            ti.SaveAndReimport();

            Debug.Log("Crest: Probe saved to " + path, AssetDatabase.LoadAssetAtPath<Object>(path));
        }
    }

    [CustomEditor(typeof(QueryEvents))]
    sealed class QueryEventsEditor : Inspector
    {
        protected override void RenderInspectorGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox
            (
                "For the Above/Below Water Surface Events, whenever this game object goes below or above the water " +
                "surface, the appropriate event is fired once per state change. It can be used to trigger audio to " +
                "play underwater and much more. For the Distance From Water Surface event, it will pass the " +
                "distance every frame (passing normalised distance to audio volume as an example).",
                MessageType.Info
            );
            EditorGUILayout.Space();

            base.RenderInspectorGUI();
        }
    }

    [CustomEditor(typeof(NetworkedTimeProvider))]
    sealed class NetworkedTimeProviderEditor : Inspector
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox($"Assign this component to the <i>{nameof(WaterRenderer)}</i> component and set the TimeOffsetToServer property of this component (at runtime from C#) to the delta from this client's time to the shared server time.", MessageType.Info);
        }
    }
}
