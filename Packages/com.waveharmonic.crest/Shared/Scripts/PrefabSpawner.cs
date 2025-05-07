// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using System.Collections.Generic;

namespace WaveHarmonic.Crest.Examples
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class PrefabSpawner : MonoBehaviour
    {
        enum Mode
        {
            OnStart,
            OnDemand,
        }


        [SerializeField]
        GameObject _Prefab;

        [SerializeField]
        Mode _Mode;

        [SerializeField]
        bool _DestroyInstances = true;

        [SerializeField]
        bool _SpawnAsChild = true;

        [SerializeField]
        bool _RandomizePosition;

        [SerializeField]
        float _RandomizePositionSphericalSize = 1f;


        readonly List<GameObject> _Instances = new();


        // Start is called before the first frame update
        void Start()
        {
            if (_Mode is Mode.OnDemand) return;
            Execute();
        }

        void OnDestroy()
        {
            if (!_DestroyInstances)
            {
                return;
            }

            foreach (var instance in _Instances)
            {
                Destroy(instance);
            }
        }

        public void Execute()
        {
            var prefab = Instantiate(_Prefab);
            prefab.transform.SetPositionAndRotation(transform.position +
                (_RandomizePosition ? Random.insideUnitSphere * _RandomizePositionSphericalSize : Vector3.zero), transform.rotation);
            prefab.transform.localScale = transform.localScale;
            if (_SpawnAsChild) prefab.transform.SetParent(transform, worldPositionStays: true);
            _Instances.Add(prefab);
        }
    }
}
