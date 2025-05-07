// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Debug draw crosses in an area around the GameObject on the water surface.
    /// </summary>
    [@ExecuteDuringEditMode]
    [AddComponentMenu(Constants.k_MenuPrefixDebug + "Collision Area Visualizer")]
    sealed class CollisionAreaVisualizer : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [Tooltip(ICollisionProvider.k_LayerTooltip)]
        [SerializeField]
        internal CollisionLayer _Layer;

        [SerializeField]
        float _ObjectWidth = 0f;

        [SerializeField]
        float _StepSize = 5f;

        [SerializeField]
        int _Steps = 10;

        [SerializeField]
        bool _UseDisplacements;

        [SerializeField]
        bool _UseNormals;

        float[] _ResultHeights;
        Vector3[] _ResultDisplacements;
        Vector3[] _ResultNormals;
        Vector3[] _SamplePositions;

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (water.AnimatedWavesLod.Provider == null)
            {
                return;
            }

            if (_ResultHeights == null || _ResultHeights.Length != _Steps * _Steps)
            {
                _ResultHeights = new float[_Steps * _Steps];
            }
            if (_ResultDisplacements == null || _ResultDisplacements.Length != _Steps * _Steps)
            {
                _ResultDisplacements = new Vector3[_Steps * _Steps];
            }
            if (_ResultNormals == null || _ResultNormals.Length != _Steps * _Steps)
            {
                _ResultNormals = new Vector3[_Steps * _Steps];

                for (var i = 0; i < _ResultNormals.Length; i++)
                {
                    _ResultNormals[i] = Vector3.up;
                }
            }
            if (_SamplePositions == null || _SamplePositions.Length != _Steps * _Steps)
            {
                _SamplePositions = new Vector3[_Steps * _Steps];
            }

            var collProvider = water.AnimatedWavesLod.Provider;

            for (var i = 0; i < _Steps; i++)
            {
                for (var j = 0; j < _Steps; j++)
                {
                    _SamplePositions[j * _Steps + i] = new(((i + 0.5f) - _Steps / 2f) * _StepSize, 0f, ((j + 0.5f) - _Steps / 2f) * _StepSize);
                    _SamplePositions[j * _Steps + i].x += transform.position.x;
                    _SamplePositions[j * _Steps + i].z += transform.position.z;
                }
            }

            if (_UseDisplacements)
            {
                if (collProvider.RetrieveSucceeded(collProvider.Query(GetHashCode(), _ObjectWidth, _SamplePositions, _ResultDisplacements, _UseNormals ? _ResultNormals : null, null, _Layer)))
                {
                    for (var i = 0; i < _Steps; i++)
                    {
                        for (var j = 0; j < _Steps; j++)
                        {
                            var result = _SamplePositions[j * _Steps + i];
                            result.y = water.SeaLevel;
                            result += _ResultDisplacements[j * _Steps + i];

                            var norm = _UseNormals ? _ResultNormals[j * _Steps + i] : Vector3.up;

                            DebugDrawCross(result, norm, Mathf.Min(_StepSize / 4f, 1f), Color.green);
                        }
                    }
                }
            }
            else
            {
                if (collProvider.RetrieveSucceeded(collProvider.Query(GetHashCode(), _ObjectWidth, _SamplePositions, _ResultHeights, _UseNormals ? _ResultNormals : null, null, _Layer)))
                {
                    for (var i = 0; i < _Steps; i++)
                    {
                        for (var j = 0; j < _Steps; j++)
                        {
                            var result = _SamplePositions[j * _Steps + i];
                            result.y = _ResultHeights[j * _Steps + i];

                            var norm = _UseNormals ? _ResultNormals[j * _Steps + i] : Vector3.up;

                            DebugDrawCross(result, norm, Mathf.Min(_StepSize / 4f, 1f), Color.green);
                        }
                    }
                }
            }
        }

        public static void DebugDrawCross(Vector3 pos, float r, Color col, float duration = 0f)
        {
            Debug.DrawLine(pos - Vector3.up * r, pos + Vector3.up * r, col, duration);
            Debug.DrawLine(pos - Vector3.right * r, pos + Vector3.right * r, col, duration);
            Debug.DrawLine(pos - Vector3.forward * r, pos + Vector3.forward * r, col, duration);
        }

        public static void DebugDrawCross(Vector3 pos, Vector3 up, float r, Color col, float duration = 0f)
        {
            up.Normalize();
            var right = Vector3.Normalize(Vector3.Cross(up, Vector3.forward));
            var forward = Vector3.Cross(up, right);
            Debug.DrawLine(pos - up * r, pos + up * r, col, duration);
            Debug.DrawLine(pos - right * r, pos + right * r, col, duration);
            Debug.DrawLine(pos - forward * r, pos + forward * r, col, duration);
        }
    }
}
