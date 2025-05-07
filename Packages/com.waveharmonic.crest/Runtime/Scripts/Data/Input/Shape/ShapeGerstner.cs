// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Gerstner wave shape.
    /// </summary>
    [AddComponentMenu(Constants.k_MenuPrefixInputs + "Shape Gerstner")]
    public sealed partial class ShapeGerstner : ShapeWaves
    {
        // Waves

        [Tooltip("The weight of the opposing, second pair of Gerstner waves.\n\nEach Gerstner wave is actually a pair of waves travelling in opposite directions (similar to FFT). This weight is applied to the wave travelling in against-wind direction. Set to zero to obtain simple single waves which are useful for shorelines waves.")]
        [@Range(0f, 1f, order = -3)]
        [@GenerateAPI]
        [SerializeField]
        float _ReverseWaveWeight = 0.5f;


        // Generation Settings

        [Tooltip("How many wave components to generate in each octave.")]
        [@Delayed]
        [@GenerateAPI]
        [SerializeField]
        int _ComponentsPerOctave = 8;

        [Tooltip("Change to get a different set of waves.")]
        [@GenerateAPI]
        [SerializeField]
        int _RandomSeed = 0;

        private protected override int MinimumResolution => 8;
        private protected override int MaximumResolution => 64;

        float _WindSpeedWhenGenerated = -1f;

        const int k_MaximumWaveComponents = 1024;

        // Data for all components
        float[] _Wavelengths;
        float[] _Amplitudes;
        float[] _Amplitudes2;
        float[] _Powers;
        float[] _AngleDegrees;
        float[] _Phases;
        float[] _Phases2;

        struct GerstnerCascadeParams
        {
            public int _StartIndex;
        }
        ComputeBuffer _BufferCascadeParameters;
        readonly GerstnerCascadeParams[] _CascadeParameters = new GerstnerCascadeParams[k_CascadeCount + 1];

        // Caution - order here impact performance. Rearranging these to match order
        // they're read in the compute shader made it 50% slower..
        struct GerstnerWaveComponent4
        {
            public Vector4 _TwoPiOverWavelength;
            public Vector4 _Amplitude;
            public Vector4 _WaveDirectionX;
            public Vector4 _WaveDirectionZ;
            public Vector4 _Omega;
            public Vector4 _Phase;
            public Vector4 _ChopAmplitude;
            // Waves are generated in pairs, these values are for the second in the pair
            public Vector4 _Amplitude2;
            public Vector4 _ChopAmplitude2;
            public Vector4 _Phase2;
        }
        ComputeBuffer _BufferWaveData;
        readonly GerstnerWaveComponent4[] _WaveData = new GerstnerWaveComponent4[k_MaximumWaveComponents / 4];

        ComputeShader _ShaderGerstner;
        int _KernelGerstner = -1;


        static new class ShaderIDs
        {
            public static readonly int s_FirstCascadeIndex = Shader.PropertyToID("_Crest_FirstCascadeIndex");
            public static readonly int s_TextureRes = Shader.PropertyToID("_Crest_TextureRes");
            public static readonly int s_CascadeParams = Shader.PropertyToID("_Crest_GerstnerCascadeParams");
            public static readonly int s_GerstnerWaveData = Shader.PropertyToID("_Crest_GerstnerWaveData");
        }


        readonly float _TwoPi = 2f * Mathf.PI;
        readonly float _ReciprocalTwoPi = 1f / (2f * Mathf.PI);

        internal static readonly SortedList<int, ShapeGerstner> s_Instances = new(Helpers.SiblingIndexComparison);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStatics()
        {
            s_Instances.Clear();
        }

        void InitData()
        {
            if (_WaveBuffers == null)
            {
                _WaveBuffers = new(_Resolution, _Resolution, 0, GraphicsFormat.R16G16B16A16_SFloat);
            }
            else
            {
                _WaveBuffers.Release();
            }

            {
                _WaveBuffers.width = _WaveBuffers.height = _Resolution;
                _WaveBuffers.wrapMode = TextureWrapMode.Clamp;
                _WaveBuffers.antiAliasing = 1;
                _WaveBuffers.filterMode = FilterMode.Bilinear;
                _WaveBuffers.anisoLevel = 0;
                _WaveBuffers.useMipMap = false;
                _WaveBuffers.name = "_Crest_GerstnerCascades";
                _WaveBuffers.dimension = TextureDimension.Tex2DArray;
                _WaveBuffers.volumeDepth = k_CascadeCount;
                _WaveBuffers.enableRandomWrite = true;
                _WaveBuffers.Create();
            }

            _BufferCascadeParameters?.Release();
            _BufferWaveData?.Release();

            _BufferCascadeParameters = new(k_CascadeCount + 1, UnsafeUtility.SizeOf<GerstnerCascadeParams>());
            _BufferWaveData = new(k_MaximumWaveComponents / 4, UnsafeUtility.SizeOf<GerstnerWaveComponent4>());

            _ShaderGerstner = WaterResources.Instance.Compute._Gerstner;
            _KernelGerstner = _ShaderGerstner.FindKernel("Gerstner");
        }

        private protected override void OnUpdate(WaterRenderer water)
        {
            var isFirstUpdate = _FirstUpdate;

            base.OnUpdate(water);

            if (_WaveBuffers == null || _Resolution != _WaveBuffers.width || _BufferCascadeParameters == null || _BufferWaveData == null)
            {
                InitData();
            }

            var windSpeed = WindSpeedMPS;
            if (isFirstUpdate || UpdateDataEachFrame || windSpeed != _WindSpeedWhenGenerated)
            {
                UpdateWaveData(water, windSpeed);
                _WindSpeedWhenGenerated = windSpeed;
            }

            ReportMaxDisplacement(water);
        }

        internal override void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slice = -1)
        {
            if (_LastGenerateFrameCount != Time.frameCount)
            {
                if (_FirstCascade >= 0 && _LastCascade >= 0)
                {
                    UpdateGenerateWaves(buffer);
                    // Above changes the render target. Change it back if necessary.
                    if (!IsCompute) buffer.SetRenderTarget(target, 0, CubemapFace.Unknown, slice);
                }

                _LastGenerateFrameCount = Time.frameCount;
            }

            base.Draw(lod, buffer, target, pass, weight, slice);
        }

        private protected override void SetRenderParameters<T>(WaterRenderer water, T wrapper)
        {
            base.SetRenderParameters(water, wrapper);
            wrapper.SetVector(ShapeWaves.ShaderIDs.s_AxisX, PrimaryWaveDirection);
        }

        void SliceUpWaves(WaterRenderer water, float windSpeed)
        {
            // Do not filter cascades if blending as the blend operation might be skipped.
            // Same for renderer as we do not know the blend operation.
            var isFilterable = Blend != LodInputBlend.Alpha && _Mode != LodInputMode.Renderer;

            _FirstCascade = isFilterable ? -1 : 0;
            _LastCascade = -2;

            var cascadeIdx = 0;
            var componentIdx = 0;
            var outputIdx = 0;
            _CascadeParameters[0]._StartIndex = 0;

            // Seek forward to first wavelength that is big enough to render into current cascades
            var minWl = MinWavelength(cascadeIdx);
            while (componentIdx < _Wavelengths.Length && _Wavelengths[componentIdx] < minWl)
            {
                componentIdx++;
            }
            //Debug.Log($"Crest: {cascadeIdx}: start {_cascadeParams[cascadeIdx]._startIndex} minWL {minWl}");

            for (; componentIdx < _Wavelengths.Length; componentIdx++)
            {
                // Skip small amplitude waves
                while (componentIdx < _Wavelengths.Length && _Amplitudes[componentIdx] < 0.001f)
                {
                    componentIdx++;
                }
                if (componentIdx >= _Wavelengths.Length) break;

                // Check if we need to move to the next cascade
                while (cascadeIdx < k_CascadeCount && _Wavelengths[componentIdx] >= 2f * minWl)
                {
                    // Wrap up this cascade and begin next

                    // Fill remaining elements of current vector4 with 0s
                    var vi = outputIdx / 4;
                    var ei = outputIdx - vi * 4;

                    while (ei != 0)
                    {
                        _WaveData[vi]._TwoPiOverWavelength[ei] = 1f;
                        _WaveData[vi]._Amplitude[ei] = 0f;
                        _WaveData[vi]._WaveDirectionX[ei] = 0f;
                        _WaveData[vi]._WaveDirectionZ[ei] = 0f;
                        _WaveData[vi]._Omega[ei] = 0f;
                        _WaveData[vi]._Phase[ei] = 0f;
                        _WaveData[vi]._Phase2[ei] = 0f;
                        _WaveData[vi]._ChopAmplitude[ei] = 0f;
                        _WaveData[vi]._Amplitude2[ei] = 0f;
                        _WaveData[vi]._ChopAmplitude2[ei] = 0f;
                        ei = (ei + 1) % 4;
                        outputIdx++;
                    }

                    if (outputIdx > 0 && _FirstCascade < 0) _FirstCascade = cascadeIdx;

                    cascadeIdx++;
                    _CascadeParameters[cascadeIdx]._StartIndex = outputIdx / 4;
                    minWl *= 2f;

                    //Debug.Log($"Crest: {cascadeIdx}: start {_cascadeParams[cascadeIdx]._startIndex} minWL {minWl}");
                }
                if (cascadeIdx == k_CascadeCount) break;

                {
                    // Pack into vector elements
                    var vi = outputIdx / 4;
                    var ei = outputIdx - vi * 4;

                    _WaveData[vi]._Amplitude[ei] = _Amplitudes[componentIdx];
                    _WaveData[vi]._Amplitude2[ei] = _Amplitudes2[componentIdx];

                    var chopScale = _ActiveSpectrum._ChopScales[componentIdx / _ComponentsPerOctave];
                    _WaveData[vi]._ChopAmplitude[ei] = -chopScale * _ActiveSpectrum._Chop * _Amplitudes[componentIdx];
                    _WaveData[vi]._ChopAmplitude2[ei] = -chopScale * _ActiveSpectrum._Chop * _Amplitudes2[componentIdx];

                    var angle = Mathf.Deg2Rad * _AngleDegrees[componentIdx];
                    var dx = Mathf.Cos(angle);
                    var dz = Mathf.Sin(angle);

                    var gravityScale = _ActiveSpectrum._GravityScales[componentIdx / _ComponentsPerOctave];
                    var gravity = water.Gravity * _ActiveSpectrum._GravityScale;
                    var c = Mathf.Sqrt(_Wavelengths[componentIdx] * gravity * gravityScale * _ReciprocalTwoPi);
                    var k = _TwoPi / _Wavelengths[componentIdx];

                    // Constrain wave vector (wavelength and wave direction) to ensure wave tiles across domain
                    {
                        var kx = k * dx;
                        var kz = k * dz;
                        var diameter = 0.5f * (1 << cascadeIdx);

                        // Number of times wave repeats across domain in x and z
                        var n = kx / (_TwoPi / diameter);
                        var m = kz / (_TwoPi / diameter);
                        // Ensure the wave repeats an integral number of times across domain
                        kx = _TwoPi * Mathf.Round(n) / diameter;
                        kz = _TwoPi * Mathf.Round(m) / diameter;

                        // Compute new wave vector and direction
                        k = Mathf.Sqrt(kx * kx + kz * kz);
                        dx = kx / k;
                        dz = kz / k;
                    }

                    _WaveData[vi]._TwoPiOverWavelength[ei] = k;
                    _WaveData[vi]._WaveDirectionX[ei] = dx;
                    _WaveData[vi]._WaveDirectionZ[ei] = dz;

                    // Repeat every 2pi to keep angle bounded - helps precision on 16bit platforms
                    _WaveData[vi]._Omega[ei] = k * c;
                    _WaveData[vi]._Phase[ei] = Mathf.Repeat(_Phases[componentIdx], Mathf.PI * 2f);
                    _WaveData[vi]._Phase2[ei] = Mathf.Repeat(_Phases2[componentIdx], Mathf.PI * 2f);

                    outputIdx++;
                }
            }

            _LastCascade = isFilterable ? cascadeIdx : k_CascadeCount - 1;

            {
                // Fill remaining elements of current vector4 with 0s
                var vi = outputIdx / 4;
                var ei = outputIdx - vi * 4;

                while (ei != 0)
                {
                    _WaveData[vi]._TwoPiOverWavelength[ei] = 1f;
                    _WaveData[vi]._Amplitude[ei] = 0f;
                    _WaveData[vi]._WaveDirectionX[ei] = 0f;
                    _WaveData[vi]._WaveDirectionZ[ei] = 0f;
                    _WaveData[vi]._Omega[ei] = 0f;
                    _WaveData[vi]._Phase[ei] = 0f;
                    _WaveData[vi]._Phase2[ei] = 0f;
                    _WaveData[vi]._ChopAmplitude[ei] = 0f;
                    _WaveData[vi]._Amplitude2[ei] = 0f;
                    _WaveData[vi]._ChopAmplitude2[ei] = 0f;
                    ei = (ei + 1) % 4;
                    outputIdx++;
                }
            }

            while (cascadeIdx < k_CascadeCount)
            {
                cascadeIdx++;
                minWl *= 2f;
                _CascadeParameters[cascadeIdx]._StartIndex = outputIdx / 4;
                //Debug.Log($"Crest: {cascadeIdx}: start {_cascadeParams[cascadeIdx]._startIndex} minWL {minWl}");
            }

            _BufferCascadeParameters.SetData(_CascadeParameters);
            _BufferWaveData.SetData(_WaveData);
        }

        void UpdateGenerateWaves(CommandBuffer buf)
        {
            // Clear existing waves or they could get copied.
            buf.SetRenderTarget(_WaveBuffers, 0, CubemapFace.Unknown, -1);
            buf.ClearRenderTarget(RTClearFlags.Color, Color.black, 0, 0);
            buf.SetComputeFloatParam(_ShaderGerstner, ShaderIDs.s_TextureRes, _WaveBuffers.width);
            buf.SetComputeIntParam(_ShaderGerstner, ShaderIDs.s_FirstCascadeIndex, _FirstCascade);
            buf.SetComputeBufferParam(_ShaderGerstner, _KernelGerstner, ShaderIDs.s_CascadeParams, _BufferCascadeParameters);
            buf.SetComputeBufferParam(_ShaderGerstner, _KernelGerstner, ShaderIDs.s_GerstnerWaveData, _BufferWaveData);
            buf.SetComputeTextureParam(_ShaderGerstner, _KernelGerstner, ShapeWaves.ShaderIDs.s_WaveBuffer, _WaveBuffers);

            buf.DispatchCompute(_ShaderGerstner, _KernelGerstner, _WaveBuffers.width / Lod.k_ThreadGroupSizeX, _WaveBuffers.height / Lod.k_ThreadGroupSizeY, _LastCascade - _FirstCascade + 1);
        }

        /// <summary>
        /// Resamples wave spectrum
        /// </summary>
        /// <param name="water">The water renderer.</param>
        /// <param name="windSpeed">Wind speed in m/s</param>
        void UpdateWaveData(WaterRenderer water, float windSpeed)
        {
            // Set random seed to get repeatable results
            var randomStateBkp = Random.state;
            Random.InitState(_RandomSeed);

            _ActiveSpectrum.GenerateWaveData(_ComponentsPerOctave, ref _Wavelengths, ref _AngleDegrees);

            UpdateAmplitudes();

            // Won't run every time so put last in the random sequence
            if (_Phases == null || _Phases.Length != _Wavelengths.Length || _Phases2 == null || _Phases2.Length != _Wavelengths.Length)
            {
                InitPhases();
            }

            Random.state = randomStateBkp;

            SliceUpWaves(water, windSpeed);
        }

        void UpdateAmplitudes()
        {
            if (_Amplitudes == null || _Amplitudes.Length != _Wavelengths.Length)
            {
                _Amplitudes = new float[_Wavelengths.Length];
            }
            if (_Amplitudes2 == null || _Amplitudes2.Length != _Wavelengths.Length)
            {
                _Amplitudes2 = new float[_Wavelengths.Length];
            }
            if (_Powers == null || _Powers.Length != _Wavelengths.Length)
            {
                _Powers = new float[_Wavelengths.Length];
            }

            var windSpeed = WindSpeedMPS;

            for (var i = 0; i < _Wavelengths.Length; i++)
            {
                var amp = _ActiveSpectrum.GetAmplitude(_Wavelengths[i], _ComponentsPerOctave, windSpeed, out _Powers[i]);
                _Amplitudes[i] = Random.value * amp;
                _Amplitudes2[i] = Random.value * amp * _ReverseWaveWeight;
            }
        }

        void InitPhases()
        {
            // Set random seed to get repeatable results
            var randomStateBkp = Random.state;
            Random.InitState(_RandomSeed);

            var totalComps = _ComponentsPerOctave * WaveSpectrum.k_NumberOfOctaves;
            _Phases = new float[totalComps];
            _Phases2 = new float[totalComps];
            for (var octave = 0; octave < WaveSpectrum.k_NumberOfOctaves; octave++)
            {
                for (var i = 0; i < _ComponentsPerOctave; i++)
                {
                    var index = octave * _ComponentsPerOctave + i;
                    var rnd = (i + Random.value) / _ComponentsPerOctave;
                    _Phases[index] = 2f * Mathf.PI * rnd;

                    var rnd2 = (i + Random.value) / _ComponentsPerOctave;
                    _Phases2[index] = 2f * Mathf.PI * rnd2;
                }
            }

            Random.state = randomStateBkp;
        }

        private protected override void ReportMaxDisplacement(WaterRenderer water)
        {
            if (!Enabled) return;

            if (_ActiveSpectrum._ChopScales.Length != WaveSpectrum.k_NumberOfOctaves)
            {
                Debug.LogError($"Crest: {nameof(WaveSpectrum)} {_ActiveSpectrum.name} is out of date, please open this asset and resave in editor.", _ActiveSpectrum);
            }

            if (_Wavelengths == null)
            {
                return;
            }

            var ampSum = 0f;
            for (var i = 0; i < _Wavelengths.Length; i++)
            {
                ampSum += _Amplitudes[i] * _ActiveSpectrum._ChopScales[i / _ComponentsPerOctave];
            }

            // Apply weight or will cause popping due to scale change.
            ampSum *= Weight;

            MaximumReportedHorizontalDisplacement = ampSum * _ActiveSpectrum._Chop;
            MaximumReportedVerticalDisplacement = ampSum;
            MaximumReportedWavesDisplacement = ampSum;

            if (Mode == LodInputMode.Global)
            {
                water.ReportMaximumDisplacement(ampSum * _ActiveSpectrum._Chop, ampSum, ampSum);
            }
        }

        private protected override void Initialize()
        {
            base.Initialize();
            s_Instances.Add(transform.GetSiblingIndex(), this);
        }

        private protected override void OnDisable()
        {
            base.OnDisable();

            s_Instances.Remove(this);

            if (_BufferCascadeParameters != null && _BufferCascadeParameters.IsValid())
            {
                _BufferCascadeParameters.Dispose();
                _BufferCascadeParameters = null;
            }
            if (_BufferWaveData != null && _BufferWaveData.IsValid())
            {
                _BufferWaveData.Dispose();
                _BufferWaveData = null;
            }

            if (_WaveBuffers != null)
            {
                Helpers.Destroy(_WaveBuffers);
                _WaveBuffers = null;
            }
        }

        private protected override void DestroySharedResources() { }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (_DrawSlicesInEditor && _WaveBuffers != null && _WaveBuffers.IsCreated())
            {
                DebugGUI.DrawTextureArray(_WaveBuffers, 8, 0.5f);
            }
        }
#endif
    }

    partial class ShapeGerstner : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 1;
#pragma warning restore 414

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _Version = MigrateV1(_Version);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // Empty.
        }
    }
}
