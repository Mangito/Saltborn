// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Overrides global quality settings.
    /// </summary>
    [System.Serializable]
    public sealed partial class QualitySettingsOverride
    {
        [Tooltip("Whether to override the LOD bias.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _OverrideLodBias;

        [Tooltip("Overrides the LOD bias for meshes.\n\nHighest quality is infinity.")]
        [@Predicated(nameof(_OverrideLodBias))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal float _LodBias;

        [Tooltip("Whether to override the maximum LOD level.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _OverrideMaximumLodLevel;

        [Tooltip("Overrides the maximum LOD level.\n\nHighest quality is zero.")]
        [@Predicated(nameof(_OverrideMaximumLodLevel))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal int _MaximumLodLevel;


        [Tooltip("Whether to override the terrain pixel error.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _OverrideTerrainPixelError;

        [Tooltip("Overrides the pixel error value for terrains.\n\nHighest quality is zero.")]
        [@Predicated(nameof(_OverrideTerrainPixelError))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal float _TerrainPixelError;

        float _OldLodBias;
        int _OldMaximumLodLevelOverride;
        float _OldTerrainPixelError;
        TerrainQualityOverrides _OldTerrainOverrides;

        internal void Override()
        {
            if (_OverrideLodBias)
            {
                _OldLodBias = QualitySettings.lodBias;
                QualitySettings.lodBias = _LodBias;
            }

            if (_OverrideMaximumLodLevel)
            {
                _OldMaximumLodLevelOverride = QualitySettings.maximumLODLevel;
                QualitySettings.maximumLODLevel = _MaximumLodLevel;
            }

            if (_OverrideTerrainPixelError)
            {
                _OldTerrainOverrides = QualitySettings.terrainQualityOverrides;
                _OldTerrainPixelError = QualitySettings.terrainPixelError;
                QualitySettings.terrainQualityOverrides = TerrainQualityOverrides.PixelError;
                QualitySettings.terrainPixelError = _TerrainPixelError;
            }
        }

        internal void Restore()
        {
            if (_OverrideLodBias)
            {
                QualitySettings.lodBias = _OldLodBias;
            }

            if (_OverrideMaximumLodLevel)
            {
                QualitySettings.maximumLODLevel = _OldMaximumLodLevelOverride;
            }

            if (_OverrideTerrainPixelError)
            {
                QualitySettings.terrainQualityOverrides = _OldTerrainOverrides;
                QualitySettings.terrainPixelError = _OldTerrainPixelError;
            }
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = Hash.CreateHash();
            Hash.AddBool(_OverrideLodBias, ref hash);
            Hash.AddFloat(_LodBias, ref hash);
            Hash.AddBool(_OverrideMaximumLodLevel, ref hash);
            Hash.AddInt(_MaximumLodLevel, ref hash);
            Hash.AddBool(_OverrideTerrainPixelError, ref hash);
            Hash.AddFloat(_TerrainPixelError, ref hash);
            return hash;
        }
    }
}
