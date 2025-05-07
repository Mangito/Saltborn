// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Water shape representation - power values for each octave of wave components.
    /// </summary>
    [CreateAssetMenu(fileName = "Waves", menuName = "Crest/Wave Spectrum", order = 10000)]
    [@HelpURL("Manual/Waves.html#wave-conditions")]
    public sealed partial class WaveSpectrum : ScriptableObject
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        // These must match corresponding constants in FFTSpectrum.compute
        internal const int k_NumberOfOctaves = 14;
        internal const float k_SmallestWavelengthPower2 = -4f;

        internal static readonly float s_MinimumPowerLog = -8f;
        internal static readonly float s_MaximumPowerLog = 5f;

        [Tooltip("Variance of wave directions, in degrees.")]
        [@Range(0f, 180f)]
        [SerializeField, HideInInspector]
        internal float _WaveDirectionVariance = 90f;

        [Tooltip("More gravity means faster waves.")]
        [@Range(0f, 25f)]
        [SerializeField, HideInInspector]
        internal float _GravityScale = 1f;

        [Tooltip("Multiplier which scales waves")]
        [@Range(0f, 10f)]
        [SerializeField]
        internal float _Multiplier = 1f;

        [SerializeField, HideInInspector]
        internal float[] _PowerLogarithmicScales = new float[k_NumberOfOctaves] { -7.10794f, -6.42794f, -5.93794f, -5.27794f, -4.67794f, -3.71794f, -3.17794f, -2.60794f, -1.93794f, -1.11794f, -0.85794f, -0.36794f, 0.04206f, -8f };

        [SerializeField, HideInInspector]
        internal bool[] _PowerDisabled = new bool[k_NumberOfOctaves];

        [SerializeField, HideInInspector]
        internal float[] _ChopScales = new float[k_NumberOfOctaves] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        [SerializeField, HideInInspector]
        internal float[] _GravityScales = new float[k_NumberOfOctaves] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        [Tooltip("Scales horizontal displacement")]
        [@Range(0f, 2f)]
        [SerializeField]
        internal float _Chop = 1.6f;

#pragma warning disable 414
        [SerializeField, HideInInspector]
        internal bool _ShowAdvancedControls = false;
#pragma warning restore 414

#pragma warning disable 414
        // We need to serialize if we want undo/redo.
        [SerializeField, HideInInspector]
        internal SpectrumModel _Model;
#pragma warning restore 414

        internal enum SpectrumModel
        {
            None,
            PiersonMoskowitz,
        }

        internal static float SmallWavelength(float octaveIndex) => Mathf.Pow(2f, k_SmallestWavelengthPower2 + octaveIndex);

        static int GetOctaveIndex(float wavelength)
        {
            Debug.AssertFormat(wavelength > 0f, "Crest: {0} wavelength must be > 0.", nameof(WaveSpectrum));
            var wl_pow2 = Mathf.Log(wavelength) / Mathf.Log(2f);
            return (int)(wl_pow2 - k_SmallestWavelengthPower2);
        }

        /// <summary>
        /// Returns the amplitude of a wave described by wavelength.
        /// </summary>
        /// <param name="wavelength">Wavelength in m</param>
        /// <param name="componentsPerOctave">How many waves we're sampling, used to conserve energy for different sampling rates</param>
        /// <param name="windSpeed">Wind speed in m/s</param>
        /// <param name="power">The energy of the wave in J</param>
        /// <returns>The amplitude of the wave in m</returns>
        internal float GetAmplitude(float wavelength, float componentsPerOctave, float windSpeed, out float power)
        {
            Debug.AssertFormat(wavelength > 0f, this, "Crest: {0} wavelength must be > 0.", nameof(WaveSpectrum));

            var wl_pow2 = Mathf.Log(wavelength) / Mathf.Log(2f);
            wl_pow2 = Mathf.Clamp(wl_pow2, k_SmallestWavelengthPower2, k_SmallestWavelengthPower2 + k_NumberOfOctaves - 1f);

            var lower = Mathf.Pow(2f, Mathf.Floor(wl_pow2));

            var index = (int)(wl_pow2 - k_SmallestWavelengthPower2);

            if (_PowerLogarithmicScales.Length < k_NumberOfOctaves || _PowerDisabled.Length < k_NumberOfOctaves)
            {
                Debug.LogWarning($"Crest: Wave spectrum {name} is out of date, please open this asset and resave in editor.", this);
            }

            if (index >= _PowerLogarithmicScales.Length || index >= _PowerDisabled.Length)
            {
                Debug.AssertFormat(index < _PowerLogarithmicScales.Length && index < _PowerDisabled.Length, this, $"Crest: {0} index {index} is out of range.", nameof(WaveSpectrum));
                power = 0f;
                return 0f;
            }

            // Get the first power for interpolation if available
            var thisPower = !_PowerDisabled[index] ? _PowerLogarithmicScales[index] : s_MinimumPowerLog;

            // Get the next power for interpolation if available
            var nextIndex = index + 1;
            var hasNextIndex = nextIndex < _PowerLogarithmicScales.Length;
            var nextPower = hasNextIndex && !_PowerDisabled[nextIndex] ? _PowerLogarithmicScales[nextIndex] : s_MinimumPowerLog;

            // The amplitude calculation follows this nice paper from Frechot:
            // https://hal.archives-ouvertes.fr/file/index/docid/307938/filename/frechot_realistic_simulation_of_ocean_surface_using_wave_spectra.pdf
            var wl_lo = Mathf.Pow(2f, Mathf.Floor(wl_pow2));
            var k_lo = 2f * Mathf.PI / wl_lo;
            var c_lo = ComputeWaveSpeed(wl_lo);
            var omega_lo = k_lo * c_lo;
            var wl_hi = 2f * wl_lo;
            var k_hi = 2f * Mathf.PI / wl_hi;
            var c_hi = ComputeWaveSpeed(wl_hi);
            var omega_hi = k_hi * c_hi;

            var domega = (omega_lo - omega_hi) / componentsPerOctave;

            // Alpha used to interpolate between power values
            var alpha = (wavelength - lower) / lower;

            // Power
            power = hasNextIndex ? Mathf.Lerp(thisPower, nextPower, alpha) : thisPower;
            power = Mathf.Pow(10f, power);

            // Empirical wind influence based on alpha-beta spectrum that underlies empirical spectra
            var gravity = _GravityScale * WaterRenderer.Instance.Gravity;

            // Zero gravity will cause NaNs, and they have always been flat.
            if (gravity <= 0f) return 0f;

            var b = 1.291f;
            var wm = 0.87f * gravity / windSpeed;
            DeepDispersion(2f * Mathf.PI / wavelength, gravity, out var w);
            power *= Mathf.Exp(-b * Mathf.Pow(wm / w, 4.0f));

            var a2 = 2f * power * domega;

            // Amplitude
            var a = Mathf.Sqrt(a2);

            // Gerstner fudge - one hack to get Gerstners looking on par with FFT
            a *= 5f;

            return a * _Multiplier;
        }

        static float ComputeWaveSpeed(float wavelength, float gravityMultiplier = 1f)
        {
            // wave speed of deep sea water waves: https://en.wikipedia.org/wiki/Wind_wave
            // https://en.wikipedia.org/wiki/Dispersion_(water_waves)#Wave_propagation_and_dispersion
            var g = WaterRenderer.Instance.Gravity * gravityMultiplier;
            var k = 2f * Mathf.PI / wavelength;
            //float h = max(depth, 0.01);
            //float cp = sqrt(abs(tanh_clamped(h * k)) * g / k);
            var cp = Mathf.Sqrt(g / k);
            return cp;
        }

        /// <summary>
        /// Samples spectrum to generate wave data. Wavelengths will be in ascending order.
        /// </summary>
        internal void GenerateWaveData(int componentsPerOctave, ref float[] wavelengths, ref float[] anglesDeg)
        {
            var totalComponents = k_NumberOfOctaves * componentsPerOctave;

            if (wavelengths == null || wavelengths.Length != totalComponents) wavelengths = new float[totalComponents];
            if (anglesDeg == null || anglesDeg.Length != totalComponents) anglesDeg = new float[totalComponents];

            var minWavelength = Mathf.Pow(2f, k_SmallestWavelengthPower2);
            var invComponentsPerOctave = 1f / componentsPerOctave;

            for (var octave = 0; octave < k_NumberOfOctaves; octave++)
            {
                for (var i = 0; i < componentsPerOctave; i++)
                {
                    var index = octave * componentsPerOctave + i;

                    // Stratified random sampling - should give a better distribution of wavelengths, and also means i can generate
                    // the wavelengths in ascending order!
                    var minWavelengthi = minWavelength + invComponentsPerOctave * minWavelength * i;
                    var maxWavelengthi = Mathf.Min(minWavelengthi + invComponentsPerOctave * minWavelength, 2f * minWavelength);
                    wavelengths[index] = Mathf.Lerp(minWavelengthi, maxWavelengthi, Random.value);

                    var rnd = (i + Random.value) * invComponentsPerOctave;
                    anglesDeg[index] = (2f * rnd - 1f) * _WaveDirectionVariance;
                }

                minWavelength *= 2f;
            }
        }

        // This applies the correct PM spectrum powers, validated against a separate implementation
        internal void ApplyPiersonMoskowitzSpectrum()
        {
            var gravity = WaterRenderer.Instance != null ? WaterRenderer.Instance.Gravity : Mathf.Abs(Physics.gravity.y);

            for (var octave = 0; octave < k_NumberOfOctaves; octave++)
            {
                var wl = SmallWavelength(octave);

                var pow = PiersonMoskowitzSpectrum(gravity, wl);

                // we store power on logarithmic scale. this does not include 0, we represent 0 as min value
                pow = Mathf.Max(pow, Mathf.Pow(10f, s_MinimumPowerLog));

                _PowerLogarithmicScales[octave] = Mathf.Log10(pow);
            }
        }

        // Alpha-beta spectrum without the beta. Beta represents wind influence and is evaluated at runtime
        // for 'current' wind conditions
        static float AlphaSpectrum(float a, float g, float w)
        {
            return a * g * g / Mathf.Pow(w, 5.0f);
        }

        static void DeepDispersion(float k, float gravity, out float w)
        {
            w = Mathf.Sqrt(gravity * k);
        }

        static float PiersonMoskowitzSpectrum(float gravity, float wavelength)
        {
            var k = 2f * Mathf.PI / wavelength;
            DeepDispersion(k, gravity, out var w);
            var phillipsConstant = 8.1e-3f;
            return AlphaSpectrum(phillipsConstant, gravity, w);
        }
    }

    sealed partial class WaveSpectrum
    {
        [System.NonSerialized]
        internal Texture2D _ControlsTexture;

        [System.NonSerialized]
        readonly Color[] _ScratchData = new Color[k_NumberOfOctaves];

        internal Texture2D ControlsTexture
        {
            get
            {
                if (_ControlsTexture == null)
                {
                    _ControlsTexture = new(k_NumberOfOctaves, 1, TextureFormat.RFloat, mipChain: false, linear: true);
                    InitializeHandControls();
                }

                return _ControlsTexture;
            }
        }

        void OnDestroy()
        {
            Helpers.Destroy(_ControlsTexture);
        }

        internal void InitializeHandControls()
        {
            for (var i = 0; i < k_NumberOfOctaves; i++)
            {
                var power = _PowerDisabled[i] ? 0f : Mathf.Pow(10f, _PowerLogarithmicScales[i]);
                power *= _Multiplier * _Multiplier;
                _ScratchData[i] = power * Color.white;
            }

            ControlsTexture.SetPixels(_ScratchData);
            ControlsTexture.Apply();
        }

        [@OnChange(skipIfInactive: false)]
        internal void OnChange(string path, object previous)
        {
            InitializeHandControls();
        }

        internal void OnGUI()
        {
            if (ControlsTexture != null)
            {
                GUI.DrawTexture(new(0f, 0f, 100f, 10f), ControlsTexture);
            }
        }
    }
}
