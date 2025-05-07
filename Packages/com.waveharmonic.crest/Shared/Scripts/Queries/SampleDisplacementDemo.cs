// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Examples
{
    /// <summary>
    /// Attach this script to any GameObject and it will create three collision probes in front of the camera
    /// </summary>
    [AddComponentMenu(Constants.k_MenuPrefixSample + "Sample Displacement Demo")]
    sealed class SampleDisplacementDemo : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [Tooltip(ICollisionProvider.k_LayerTooltip)]
        [SerializeField]
        CollisionLayer _Layer;

        [SerializeField]
        bool _TrackCamera = true;

        [UnityEngine.Range(0f, 32f)]
        [SerializeField]
        float _MinimumGridSize = 0f;


        readonly GameObject[] _MarkerObjects = new GameObject[3];
        readonly Vector3[] _MarkerPosition = new Vector3[3];
        readonly Vector3[] _ResultDisplacement = new Vector3[3];
        readonly Vector3[] _ResultNormal = new Vector3[3];
        readonly Vector3[] _ResultVelocity = new Vector3[3];
        readonly float _SamplesRadius = 5f;

        private protected override Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (_TrackCamera)
            {
                var height = Mathf.Abs(Camera.main.transform.position.y - water.SeaLevel);
                var lookAngle = Mathf.Max(Mathf.Abs(Camera.main.transform.forward.y), 0.001f);
                var offset = height / lookAngle;
                _MarkerPosition[0] = Camera.main.transform.position + Camera.main.transform.forward * offset;
                _MarkerPosition[1] = Camera.main.transform.position + Camera.main.transform.forward * offset + _SamplesRadius * Vector3.right;
                _MarkerPosition[2] = Camera.main.transform.position + Camera.main.transform.forward * offset + _SamplesRadius * Vector3.forward;
            }

            var collProvider = water.AnimatedWavesLod.Provider;

            var status = collProvider.Query(GetHashCode(), _MinimumGridSize, _MarkerPosition, _ResultDisplacement, _ResultNormal, _ResultVelocity, _Layer);

            if (collProvider.RetrieveSucceeded(status))
            {
                for (var i = 0; i < _ResultDisplacement.Length; i++)
                {
                    if (_MarkerObjects[i] == null)
                    {
                        _MarkerObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Helpers.Destroy(_MarkerObjects[i].GetComponent<Collider>());
                        _MarkerObjects[i].transform.localScale = Vector3.one * 0.5f;
                    }

                    var query = _MarkerPosition[i];
                    query.y = water.SeaLevel;

                    var disp = _ResultDisplacement[i];

                    var pos = query;
                    pos.y = disp.y;
                    Debug.DrawLine(pos, pos - disp);

                    _MarkerObjects[i].transform.SetPositionAndRotation(pos, Quaternion.FromToRotation(Vector3.up, _ResultNormal[i]));
                }

                for (var i = 0; i < _ResultNormal.Length; i++)
                {
                    Debug.DrawLine(_MarkerObjects[i].transform.position, _MarkerObjects[i].transform.position + _ResultNormal[i], Color.blue);
                }

                for (var i = 0; i < _ResultVelocity.Length; i++)
                {
                    Debug.DrawLine(_MarkerObjects[i].transform.position, _MarkerObjects[i].transform.position + _ResultVelocity[i], Color.green);
                }
            }
        }
    }
}
