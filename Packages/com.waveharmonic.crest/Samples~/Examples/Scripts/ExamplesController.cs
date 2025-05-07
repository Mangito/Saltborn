// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest.Examples
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class ExamplesController : MonoBehaviour
    {
        [@DecoratedField, SerializeField]
        KeyCode _Previous = KeyCode.Comma;

        [@DecoratedField, SerializeField]
        KeyCode _Next = KeyCode.Period;

        [SerializeField]
        List<GameObject> _Prefabs = new();

        int _Index = 0;

        public void Previous() => Cycle(true);
        public void Next() => Cycle(false);

        void OnEnable()
        {
            if (_Prefabs.Count == 0)
            {
                enabled = false;
                return;
            }

            var prefab = Instantiate(_Prefabs[_Index]);
            prefab.transform.SetParent(transform, worldPositionStays: true);
        }

        void OnDisable()
        {
            var child = transform.GetChild(0);
            if (child == null)
            {
                return;
            }

            Destroy(child.gameObject);
        }

        void Update()
        {
            if (Input.GetKeyUp(_Previous))
            {
                Previous();
            }
            else if (Input.GetKeyUp(_Next))
            {
                Next();
            }
        }

        internal void Cycle(bool isReverse = false)
        {
            _Index += isReverse ? -1 : 1;

            // Wrap index.
            if (_Index < 0) _Index = _Prefabs.Count - 1;
            if (_Index == _Prefabs.Count) _Index = 0;

            var go = transform.GetChild(0).gameObject;
            go.SetActive(false);

            Helpers.Destroy(go);

            var prefab = Instantiate(_Prefabs[_Index]);
            prefab.transform.SetParent(transform, worldPositionStays: true);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ExamplesController))]
    sealed class ExamplesControllerEditor : Editor.Inspector
    {
        protected override void RenderInspectorGUI()
        {
            base.RenderInspectorGUI();

            var target = this.target as ExamplesController;

            if (GUILayout.Button("Previous"))
            {
                target.Previous();
            }

            if (GUILayout.Button("Next"))
            {
                target.Next();
            }
        }
    }
#endif
}
