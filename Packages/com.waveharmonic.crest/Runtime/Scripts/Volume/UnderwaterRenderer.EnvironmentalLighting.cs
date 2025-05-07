// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    partial class UnderwaterRenderer
    {
        const float k_DepthOutScattering = 0.25f;

        Light _EnvironmentalLight;
        float _EnvironmentalLightIntensity;
        float _EnvironmentalAmbientIntensity;
        float _EnvironmentalReflectionIntensity;
        float _EnvironmentalFogDensity;
        float _EnvironmentalAverageDensity = 0f;
        bool _EnvironmentalInitialized = false;
        bool _EnvironmentalNeedsRestoring;

        void EnableEnvironmentalLighting()
        {
            if (!_EnvironmentalLightingEnable)
            {
                return;
            }

#if d_UnitySRP
            if (_EnvironmentalLightingVolume == null && !RenderPipelineHelper.IsLegacy)
            {
                // Create volume to weigh in underwater profile
                var go = new GameObject();
                go.transform.parent = _Water.Container.transform;
                go.hideFlags = HideFlags.HideAndDontSave;
                go.name = "Underwater Lighting Volume";
                _EnvironmentalLightingVolume = go.AddComponent<Volume>();
                _EnvironmentalLightingVolume.weight = 0;
                _EnvironmentalLightingVolume.priority = 1000;
                _EnvironmentalLightingVolume.profile = _EnvironmentalLightingVolumeProfile;
            }
#endif

            _EnvironmentalInitialized = true;
        }

        void DisableEnvironmentalLighting()
        {
            RestoreEnvironmentalLighting();

            _EnvironmentalInitialized = false;
        }

        void RestoreEnvironmentalLighting()
        {
            if (!_EnvironmentalInitialized || !_EnvironmentalNeedsRestoring)
            {
                return;
            }

            // Restore lighting settings.
            if (_EnvironmentalLight != null) _EnvironmentalLight.intensity = _EnvironmentalLightIntensity;
            _EnvironmentalLight = null;
            RenderSettings.ambientIntensity = _EnvironmentalAmbientIntensity;
            RenderSettings.reflectionIntensity = _EnvironmentalReflectionIntensity;
            RenderSettings.fogDensity = _EnvironmentalFogDensity;
            Shader.SetGlobalFloat(ShaderIDs.s_UnderwaterEnvironmentalLightingWeight, 0f);
            if (_EnvironmentalLightingVolume != null) _EnvironmentalLightingVolume.weight = 0;

            _EnvironmentalNeedsRestoring = false;
        }

        void UpdateEnvironmentalLighting(Camera camera, Vector3 extinction, float height)
        {
            if (!_EnvironmentalInitialized)
            {
                return;
            }

            if (!_Water.Material.HasColor(WaterRenderer.ShaderIDs.s_AbsorptionColor))
            {
                return;
            }

            // Store lighting settings.
            {
                _EnvironmentalLight = _Water.PrimaryLight;
                if (_EnvironmentalLight) _EnvironmentalLightIntensity = _EnvironmentalLight.intensity;
                _EnvironmentalAmbientIntensity = RenderSettings.ambientIntensity;
                _EnvironmentalReflectionIntensity = RenderSettings.reflectionIntensity;
                _EnvironmentalFogDensity = RenderSettings.fogDensity;
            }

            var density = extinction;
            _EnvironmentalAverageDensity = (density.x + density.y + density.z) / 3f;

            var multiplier = Mathf.Exp(_EnvironmentalAverageDensity * Mathf.Min(height * k_DepthOutScattering, 0f) * _EnvironmentalLightingWeight);

            // Darken environmental lighting when viewer underwater.
            if (_EnvironmentalLight != null)
            {
                _EnvironmentalLight.intensity = Mathf.Lerp(0, _EnvironmentalLightIntensity, multiplier);
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(0, _EnvironmentalAmbientIntensity, multiplier);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0, _EnvironmentalReflectionIntensity, multiplier);
            RenderSettings.fogDensity = Mathf.Lerp(0, _EnvironmentalFogDensity, multiplier);

            Shader.SetGlobalFloat(ShaderIDs.s_UnderwaterEnvironmentalLightingWeight, 1f - multiplier);

#if d_UnitySRP
            if (_EnvironmentalLightingVolume != null)
            {
                _EnvironmentalLightingVolume.weight = 1f - multiplier;
            }
#endif
            _EnvironmentalNeedsRestoring = true;
        }
    }
}
