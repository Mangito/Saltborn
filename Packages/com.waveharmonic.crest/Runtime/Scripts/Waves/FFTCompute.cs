// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Inspired by https://github.com/speps/GX-EncinoWaves

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Runs FFT to generate water surface displacements
    /// </summary>
    sealed class FFTCompute
    {
        // Must match 'SIZE' param of first kernel in FFTCompute.compute
        const int k_Kernel0Resolution = 8;

        // Must match CASCADE_COUNT in FFTCompute.compute
        const int k_CascadeCount = 16;

        bool _Initialized = false;

        RenderTexture _SpectrumInitial;

        /// <summary>
        /// Generated 'raw', uncombined, wave data. Input for putting into AnimWaves data before combine pass.
        /// </summary>
        public RenderTexture WaveBuffers { get; private set; }

        bool _SpectrumInitialized = false;

        ComputeShader _ShaderSpectrum;
        ComputeShader _ShaderFFT;

        int _KernelSpectrumInitial;
        int _KernelSpectrumUpdate;

        Parameters _Parameters;

        float _GenerationTime = -1f;

        static readonly bool s_SupportsRandomWriteRGFloat =
            SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.RGFloat);

        public static class ShaderIDs
        {
            public static readonly int s_Size = Shader.PropertyToID("_Crest_Size");
            public static readonly int s_WindSpeed = Shader.PropertyToID("_Crest_WindSpeed");
            public static readonly int s_Turbulence = Shader.PropertyToID("_Crest_Turbulence");
            public static readonly int s_Alignment = Shader.PropertyToID("_Crest_Alignment");
            public static readonly int s_Gravity = Shader.PropertyToID("_Crest_Gravity");
            public static readonly int s_Period = Shader.PropertyToID("_Crest_Period");
            public static readonly int s_WindDir = Shader.PropertyToID("_Crest_WindDir");
            public static readonly int s_SpectrumControls = Shader.PropertyToID("_Crest_SpectrumControls");
            public static readonly int s_ResultInit = Shader.PropertyToID("_Crest_ResultInit");
            public static readonly int s_Time = Shader.PropertyToID("_Crest_Time");
            public static readonly int s_Chop = Shader.PropertyToID("_Crest_Chop");
            public static readonly int s_Init0 = Shader.PropertyToID("_Crest_Init0");
            public static readonly int s_ResultHeight = Shader.PropertyToID("_Crest_ResultHeight");
            public static readonly int s_ResultDisplaceX = Shader.PropertyToID("_Crest_ResultDisplaceX");
            public static readonly int s_ResultDisplaceZ = Shader.PropertyToID("_Crest_ResultDisplaceZ");
            public static readonly int s_InputH = Shader.PropertyToID("_Crest_InputH");
            public static readonly int s_InputX = Shader.PropertyToID("_Crest_InputX");
            public static readonly int s_InputZ = Shader.PropertyToID("_Crest_InputZ");
            public static readonly int s_InputButterfly = Shader.PropertyToID("_Crest_InputButterfly");
            public static readonly int s_Output1 = Shader.PropertyToID("_Crest_Output1");
            public static readonly int s_Output2 = Shader.PropertyToID("_Crest_Output2");
            public static readonly int s_Output3 = Shader.PropertyToID("_Crest_Output3");
            public static readonly int s_Output = Shader.PropertyToID("_Crest_Output");

            public static readonly int s_TemporaryFFT1 = Shader.PropertyToID("_Crest_TemporaryFFT1");
            public static readonly int s_TemporaryFFT2 = Shader.PropertyToID("_Crest_TemporaryFFT2");
            public static readonly int s_TemporaryFFT3 = Shader.PropertyToID("_Crest_TemporaryFFT3");
        }

        internal readonly struct Parameters
        {
            public readonly WaveSpectrum _Spectrum;
            public readonly int _Resolution;
            public readonly float _LoopPeriod;
            public readonly float _WindSpeed;
            public readonly float _WindDirectionRadians;
            public readonly float _WindTurbulence;
            public readonly float _WindAlignment;

            public Parameters(WaveSpectrum spectrum, int resolution, float period, float speed, float direction, float turbulence, float alignment)
            {
                _Spectrum = spectrum;
                _Resolution = resolution;
                _LoopPeriod = period;
                _WindSpeed = speed;
                _WindDirectionRadians = direction;
                _WindTurbulence = turbulence;
                _WindAlignment = alignment;
            }

            // Implement custom or incur allocations.
            public override int GetHashCode()
            {
                return System.HashCode.Combine(_Spectrum, _LoopPeriod, _WindSpeed, _WindDirectionRadians, _WindTurbulence, _WindAlignment, _Resolution);
            }

            public int GetHashCode(int resolution)
            {
                return System.HashCode.Combine(_Spectrum, _LoopPeriod, _WindSpeed, _WindDirectionRadians, _WindTurbulence, _WindAlignment, resolution);
            }
        }

        public FFTCompute(Parameters parameters)
        {
            Debug.Assert(Mathf.NextPowerOfTwo(parameters._Resolution) == parameters._Resolution, "Crest: FFTCompute resolution must be power of 2");
            _Parameters = parameters;
        }

        public void Release()
        {
            if (_SpectrumInitial != null)
            {
                _SpectrumInitial.Release();
            }

            if (WaveBuffers != null)
            {
                WaveBuffers.Release();
            }

            Helpers.Destroy(_SpectrumInitial);
            Helpers.Destroy(WaveBuffers);

            _SpectrumInitialized = false;
            _Initialized = false;
        }

        internal static void CleanUpAll()
        {
            foreach (var generator in s_Generators)
            {
                generator.Value.Release();
            }

            s_Generators?.Clear();

            foreach (var texture in s_ButterflyTextures?.Values)
            {
                Helpers.Destroy(texture);
            }

            s_ButterflyTextures?.Clear();
        }

        static readonly Dictionary<int, FFTCompute> s_Generators = new();
        static readonly Dictionary<int, Texture2D> s_ButterflyTextures = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStatics()
        {
            CleanUpAll();
        }

        /// <summary>
        /// Computes water surface displacement, with wave components split across slices of the output texture array
        /// </summary>
        public static RenderTexture GenerateDisplacements(CommandBuffer buf, float time, Parameters parameters, bool updateSpectrum)
        {
            var conditionsHash = parameters.GetHashCode();
            // All static data arguments should be hashed here and passed to the generator constructor
            if (!s_Generators.TryGetValue(conditionsHash, out var generator))
            {
                // No generator for these params - create one
                generator = new(parameters);
                s_Generators.Add(conditionsHash, generator);
            }

            // The remaining dynamic data arguments should be passed in to the generation here
            return generator.GenerateDisplacementsInternal(buf, time, updateSpectrum);
        }

        RenderTexture GenerateDisplacementsInternal(CommandBuffer buffer, float time, bool updateSpectrum)
        {
            // Check if already generated, and we're not being asked to re-update the spectrum
            if (_GenerationTime == time && !updateSpectrum)
            {
                return WaveBuffers;
            }

            var resolution = _Parameters._Resolution;
            var period = _Parameters._LoopPeriod;

            // Initialize.
            if (!_Initialized || _SpectrumInitial == null)
            {
                Release();

                _ShaderSpectrum = WaterResources.Instance.Compute._FFTSpectrum;
                _KernelSpectrumInitial = _ShaderSpectrum.FindKernel("SpectrumInitalize");
                _KernelSpectrumUpdate = _ShaderSpectrum.FindKernel("SpectrumUpdate");
                _ShaderFFT = WaterResources.Instance.Compute._FFT;

                var rtd = new RenderTextureDescriptor(0, 0);
                rtd.width = rtd.height = resolution;
                rtd.dimension = TextureDimension.Tex2DArray;
                rtd.enableRandomWrite = true;
                rtd.depthBufferBits = 0;
                rtd.volumeDepth = k_CascadeCount;
                rtd.colorFormat = RenderTextureFormat.ARGBFloat;
                rtd.msaaSamples = 1;

                Helpers.SafeCreateRenderTexture(ref _SpectrumInitial, rtd);
                _SpectrumInitial.name = "_Crest_FFTSpectrumInit";
                _SpectrumInitial.Create();

                // Raw wave data buffer
                WaveBuffers = new(resolution, resolution, 0, GraphicsFormat.R16G16B16A16_SFloat)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0,
                    useMipMap = false,
                    name = "_Crest_FFTCascades",
                    dimension = TextureDimension.Tex2DArray,
                    volumeDepth = k_CascadeCount,
                    enableRandomWrite = true,
                };
                WaveBuffers.Create();

                // Initialize bufferfly. Cached per resolution.
                if (!s_ButterflyTextures.ContainsKey(resolution))
                {
                    // Computes the offsets used for the FFT calculation.
                    var size = Mathf.RoundToInt(Mathf.Log(resolution, 2));
                    var colors = new Color[resolution * size];

                    int offset = 1, iterations = resolution >> 1;
                    for (var index = 0; index < size; index++)
                    {
                        var rowOffset = index * resolution;
                        {
                            int start = 0, end = 2 * offset;
                            for (var iteration = 0; iteration < iterations; iteration++)
                            {
                                var bigK = 0f;
                                for (var k = start; k < end; k += 2)
                                {
                                    var phase = 2.0f * Mathf.PI * bigK * iterations / resolution;
                                    var cos = Mathf.Cos(phase);
                                    var sin = Mathf.Sin(phase);
                                    colors[rowOffset + k / 2] = new(cos, -sin, 0, 1);
                                    colors[rowOffset + k / 2 + offset] = new(-cos, sin, 0, 1);

                                    bigK += 1f;
                                }
                                start += 4 * offset;
                                end = start + 2 * offset;
                            }
                        }
                        iterations >>= 1;
                        offset <<= 1;
                    }

                    var texture = new Texture2D(resolution, Mathf.RoundToInt(Mathf.Log(resolution, 2)), TextureFormat.RGBAFloat, false, true);
                    texture.SetPixels(colors);
                    texture.Apply();
                    s_ButterflyTextures.Add(resolution, texture);
                }

                _Initialized = true;
            }

            // Initialize spectrum.
            // Computes base spectrum values based on wind speed and turbulence and spectrum controls.
            if (!_SpectrumInitialized || updateSpectrum)
            {
                var wrapper = new PropertyWrapperCompute(buffer, _ShaderSpectrum, _KernelSpectrumInitial);
                wrapper.SetInteger(ShaderIDs.s_Size, resolution);
                wrapper.SetFloat(ShaderIDs.s_WindSpeed, _Parameters._WindSpeed);
                wrapper.SetFloat(ShaderIDs.s_Turbulence, _Parameters._WindTurbulence);
                wrapper.SetFloat(ShaderIDs.s_Alignment, _Parameters._WindAlignment);
                wrapper.SetFloat(ShaderIDs.s_Gravity, WaterRenderer.Instance.Gravity);
                wrapper.SetFloat(ShaderIDs.s_Period, period < Mathf.Infinity ? period : -1);
                wrapper.SetVector(ShaderIDs.s_WindDir, new(Mathf.Cos(_Parameters._WindDirectionRadians), Mathf.Sin(_Parameters._WindDirectionRadians)));
                wrapper.SetTexture(ShaderIDs.s_SpectrumControls, _Parameters._Spectrum.ControlsTexture);
                wrapper.SetTexture(ShaderIDs.s_ResultInit, _SpectrumInitial);
                wrapper.Dispatch(resolution / 8, resolution / 8, k_CascadeCount);

                _SpectrumInitialized = true;
            }

            // Update Spectrum.
            // Computes a spectrum for the current time which can be FFT'd into the final surface.
            {
                var wrapper = new PropertyWrapperCompute(buffer, _ShaderSpectrum, _KernelSpectrumUpdate);

                var descriptor = _SpectrumInitial.descriptor;

                if (s_SupportsRandomWriteRGFloat)
                {
                    descriptor.colorFormat = RenderTextureFormat.RGFloat;
                }

                // No need to clear as overwritten.
                buffer.GetTemporaryRT(ShaderIDs.s_TemporaryFFT1, descriptor);
                buffer.GetTemporaryRT(ShaderIDs.s_TemporaryFFT2, descriptor);
                buffer.GetTemporaryRT(ShaderIDs.s_TemporaryFFT3, descriptor);

                wrapper.SetInteger(ShaderIDs.s_Size, resolution);
                wrapper.SetFloat(ShaderIDs.s_Time, time * _Parameters._Spectrum._GravityScale);
                wrapper.SetFloat(ShaderIDs.s_Chop, _Parameters._Spectrum._Chop);
                wrapper.SetFloat(ShaderIDs.s_Period, period < Mathf.Infinity ? period : -1);
                wrapper.SetTexture(ShaderIDs.s_Init0, _SpectrumInitial);
                wrapper.SetTexture(ShaderIDs.s_ResultHeight, ShaderIDs.s_TemporaryFFT1);
                wrapper.SetTexture(ShaderIDs.s_ResultDisplaceX, ShaderIDs.s_TemporaryFFT2);
                wrapper.SetTexture(ShaderIDs.s_ResultDisplaceZ, ShaderIDs.s_TemporaryFFT3);
                wrapper.Dispatch(resolution / 8, resolution / 8, k_CascadeCount);
            }

            // Dispatch FFT.
            // FFT the spectrum into surface displacements.
            {
                var kernel = 2 * Mathf.RoundToInt(Mathf.Log(resolution / k_Kernel0Resolution, 2f));
                var wrapper = new PropertyWrapperCompute(buffer, _ShaderFFT, kernel);

                var butterfly = s_ButterflyTextures[resolution];

                wrapper.SetTexture(ShaderIDs.s_InputButterfly, butterfly);
                wrapper.SetTexture(ShaderIDs.s_Output1, ShaderIDs.s_TemporaryFFT1);
                wrapper.SetTexture(ShaderIDs.s_Output2, ShaderIDs.s_TemporaryFFT2);
                wrapper.SetTexture(ShaderIDs.s_Output3, ShaderIDs.s_TemporaryFFT3);
                wrapper.Dispatch(1, resolution, k_CascadeCount);

                wrapper = new PropertyWrapperCompute(buffer, _ShaderFFT, kernel + 1);
                wrapper.SetTexture(ShaderIDs.s_InputH, ShaderIDs.s_TemporaryFFT1);
                wrapper.SetTexture(ShaderIDs.s_InputX, ShaderIDs.s_TemporaryFFT2);
                wrapper.SetTexture(ShaderIDs.s_InputZ, ShaderIDs.s_TemporaryFFT3);
                wrapper.SetTexture(ShaderIDs.s_InputButterfly, butterfly);
                wrapper.SetTexture(ShaderIDs.s_Output, WaveBuffers);
                wrapper.Dispatch(resolution, 1, k_CascadeCount);

                buffer.ReleaseTemporaryRT(ShaderIDs.s_TemporaryFFT1);
                buffer.ReleaseTemporaryRT(ShaderIDs.s_TemporaryFFT2);
                buffer.ReleaseTemporaryRT(ShaderIDs.s_TemporaryFFT3);
            }

            _GenerationTime = time;

            return WaveBuffers;
        }

        /// <summary>
        /// Changing wave gen data can result in creating lots of new generators. This gives a way to notify
        /// that a parameter has changed. If there is no existing generator for the new param values, but there
        /// is one for the old param values, this old generator is repurposed.
        /// </summary>
        public static void OnGenerationDataUpdated(Parameters oldParameters, Parameters newParameters)
        {
            // If multiple wave components share one FFT, then one of them changes its settings, it will
            // actually steal the generator from the rest. Then the first from the rest which request the
            // old settings will trigger creation of a new generator, and the remaining ones will use this
            // new generator. In the end one new generator is created, but it's created for the old settings.
            // Generators are requested single threaded so there should not be a race condition. Odd pattern
            // but I don't think any other way works without ugly checks to see if old generators are still
            // used, or other complicated things.

            // Check if no generator exists for new values
            var newHash = newParameters.GetHashCode();
            if (!s_Generators.TryGetValue(newHash, out var oldGenerator))
            {
                // Try to adapt an existing generator rather than default to creating a new one
                // Adapting requires the resolution to the same.
                var oldHash = oldParameters.GetHashCode(newParameters._Resolution);
                if (s_Generators.TryGetValue(oldHash, out var generator))
                {
                    // Hash will change for this generator, so remove the current one
                    s_Generators.Remove(oldHash);

                    // Update params
                    generator._Parameters = newParameters;

                    // Trigger generator to re-init the spectrum
                    generator._SpectrumInitialized = false;

                    // Re-add with new hash
                    s_Generators.Add(newHash, generator);
                }
            }
            else
            {
                // There is already a new generator which will be used. Remove the previous one - if it really is needed
                // then it will be created later.
                oldGenerator.Release();
                s_Generators.Remove(oldParameters.GetHashCode());
            }
        }

        /// <summary>
        /// Number of FFT generators
        /// </summary>
        public static int GeneratorCount => s_Generators != null ? s_Generators.Count : 0;

        public static FFTCompute GetInstance(Parameters parameters)
        {
            return s_Generators.GetValueOrDefault(parameters.GetHashCode(), null);
        }

        public bool HasData()
        {
            return WaveBuffers != null && WaveBuffers.IsCreated();
        }

        internal void OnGUI()
        {
            if (WaveBuffers != null && WaveBuffers.IsCreated())
            {
                DebugGUI.DrawTextureArray(WaveBuffers, 8, 0.5f, 20f);
            }

            if (_Parameters._Spectrum != null && _Parameters._Spectrum.ControlsTexture != null)
            {
                GUI.DrawTexture(new(0f, 0f, 100f, 10f), _Parameters._Spectrum.ControlsTexture);
            }
        }
    }
}
