// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
#if CREST_DEBUG
    [CreateAssetMenu(menuName = "Crest/Resources")]
#endif

    [ExecuteAlways, Utility.FilePath("Packages/com.waveharmonic.crest/Runtime/Settings/Resources.asset")]
    [@HelpURL("Manual/Scripting.html#resources")]
    sealed class WaterResources : Utility.ScriptableSingleton<WaterResources>
    {
        [Serializable]
        public sealed class ShaderResources
        {
            // Caches camera depth as water depth.
            public Shader _CopyDepthIntoCache;

            public Shader _ColorSpline;
            public Shader _FlowSpline;
            public Shader _FoamSpline;
            public Shader _WaveSpline;

            public Shader _DepthGeometry;
            public Shader _LevelGeometry;

            public Shader _UpdateShadow;

            public Shader _UnderwaterEffect;
            public Shader _UnderwaterMask;
            public Shader _Portals;

            public Shader _ClipConvexHull;

            public Shader _ShallowWaterSimulationVisualizer;

            public Shader _DebugTextureArray;
            public Shader _Blit;
        }

        [Serializable]
        public sealed class ComputeResources
        {
            public ComputeShader _UnderwaterArtifacts;
            public ComputeShader _ShapeWavesTransfer;

            public ComputeShader _Query;

            public ComputeShader _Gerstner;
            public ComputeShader _FFT;
            public ComputeShader _FFTBake;
            public ComputeShader _FFTSpectrum;

            public ComputeShader _ShapeCombine;
            public ComputeShader _ShorelineColor;
            public ComputeShader _UpdateDynamicWaves;
            public ComputeShader _UpdateFoam;
            public ComputeShader _PackLevel;

            public ComputeShader _AbsorptionTexture;
            public ComputeShader _ClipTexture;
            public ComputeShader _FlowTexture;
            public ComputeShader _FoamTexture;
            public ComputeShader _LevelTexture;
            public ComputeShader _DepthTexture;
            public ComputeShader _ScatteringTexture;

            public ComputeShader _ClipPrimitive;
            public ComputeShader _SphereWaterInteraction;

            public ComputeShader _RenderDepthProbe;
            public ComputeShader _JumpFloodSDF;

            public ComputeShader _UpdateSWS;

            public ComputeShader _Whirlpool;

            public ComputeShader _Clear;
        }

#pragma warning disable IDE0032 // Use auto property

        [SerializeField]
        ShaderResources _Shaders = new();
        public ShaderResources Shaders => _Shaders;

        [SerializeField]
        ComputeResources _Compute = new();

#pragma warning restore IDE0032 // Use auto property

        public ComputeResources Compute => _Compute;

        public KeywordResources Keywords { get; } = new();

        public sealed class KeywordResources
        {
            public LocalKeyword AnimatedWavesTransferWavesTexture { get; private set; }
            public LocalKeyword AnimatedWavesTransferWavesTextureBlend { get; private set; }
            public LocalKeyword ClipPrimitiveInverted { get; private set; }
            public LocalKeyword ClipPrimitiveSphere { get; private set; }
            public LocalKeyword ClipPrimitiveCube { get; private set; }
            public LocalKeyword ClipPrimitiveRectangle { get; private set; }
            public LocalKeyword DepthTextureSDF { get; private set; }
            public LocalKeyword ShorelineColorSourceDistance { get; private set; }
            public LocalKeyword ShorelineColorScattering { get; private set; }
            public LocalKeyword LevelTextureCatmullRom { get; private set; }
            public LocalKeyword DepthProbeBackFaceInclusion { get; private set; }
            public LocalKeyword JumpFloodInverted { get; private set; }
            public LocalKeyword JumpFloodStandalone { get; private set; }

            internal void Initialize(WaterResources resources)
            {
                var compute = resources.Compute;

                {
                    var keywords = compute._ShapeWavesTransfer.keywordSpace;
                    AnimatedWavesTransferWavesTexture = keywords.FindKeyword("d_Texture");
                    AnimatedWavesTransferWavesTextureBlend = keywords.FindKeyword("d_TextureBlend");
                }

                {
                    var keywords = compute._ClipPrimitive.keywordSpace;
                    ClipPrimitiveInverted = keywords.FindKeyword("d_Inverted");
                    ClipPrimitiveSphere = keywords.FindKeyword("d_Sphere");
                    ClipPrimitiveCube = keywords.FindKeyword("d_Cube");
                    ClipPrimitiveRectangle = keywords.FindKeyword("d_Rectangle");
                }

                {
                    var keywords = compute._DepthTexture.keywordSpace;
                    DepthTextureSDF = keywords.FindKeyword("d_CrestSDF");
                }

                {
                    var keywords = compute._LevelTexture.keywordSpace;
                    LevelTextureCatmullRom = keywords.FindKeyword("d_CatmullRom");
                }

                {
                    var keywords = compute._RenderDepthProbe.keywordSpace;
                    DepthProbeBackFaceInclusion = keywords.FindKeyword("d_Crest_BackFaceInclusion");
                }

                {
                    var keywords = compute._JumpFloodSDF.keywordSpace;
                    JumpFloodInverted = keywords.FindKeyword("d_Crest_Inverted");
                    JumpFloodStandalone = keywords.FindKeyword("d_Crest_Standalone");
                }

                {
                    var keywords = compute._ShorelineColor.keywordSpace;
                    ShorelineColorSourceDistance = keywords.FindKeyword("d_Crest_ShorelineColorSource_ShorelineDistance");
                    ShorelineColorScattering = keywords.FindKeyword("d_Crest_ShorelineScattering");
                }
            }
        }

        public event Action AfterEnabled;

        void OnEnable()
        {
#if !CREST_DEBUG
            hideFlags = HideFlags.NotEditable;
#endif
            Keywords.Initialize(this);

            AfterEnabled?.Invoke();
        }

#if UNITY_EDITOR
        // AssetPostprocessor cannot be nested in a generic type so this cannot be moved to abstraction.
        sealed class AssetPostprocessor : UnityEditor.AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom, bool domainReload)
            {
                // Unused.
                _ = imported; _ = deleted; _ = movedTo; _ = movedFrom;

                if (domainReload)
                {
                    LoadFromAsset();
                }

                foreach (var path in imported)
                {
                    if (path.StartsWith("Packages/com.waveharmonic.crest") && path.EndsWith(".compute"))
                    {
                        // Unity loses these if the compute shader is recompiled.
                        Instance.Keywords.Initialize(Instance);
                    }
                }
            }
        }
#endif
    }
}
