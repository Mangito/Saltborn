// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

#if UNITY_EDITOR

using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WaveHarmonic.Crest.Internal
{
    using Include = ExecuteDuringEditMode.Include;

    /// <summary>
    /// Implements custom behaviours common to all components.
    /// </summary>
    public abstract partial class EditorBehaviour : MonoBehaviour
    {
        bool _IsFirstOnValidate = true;
        internal bool _IsPrefabStageInstance;

        private protected virtual void Awake()
        {
            // When copy and pasting from one scene to another, destroy instance objects as
            // they will have bad state.
            foreach (var generated in transform.GetComponentsInChildren<ManagedGameObject>(includeInactive: true))
            {
                if (generated.Owner == this)
                {
                    Helpers.Destroy(generated.gameObject);
                }
            }
        }

        /// <summary>
        /// Start method. Must be called if overriden.
        /// </summary>
        protected virtual void Start()
        {
            if (Application.isPlaying && !(bool)s_ExecuteValidators.Invoke(null, new object[] { this }))
            {
                enabled = false;
            }
        }

        // Unity does not call OnDisable/OnEnable on Reset.
        private protected virtual void Reset()
        {
            if (!enabled) return;
            enabled = false;
            enabled = true;
        }

        /// <summary>
        /// OnValidate method. Must be called if overriden.
        /// </summary>
        private protected virtual void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (_IsFirstOnValidate)
            {
                var attribute = Helpers.GetCustomAttribute<ExecuteDuringEditMode>(GetType());

                var enableInEditMode = attribute != null;

                if (enableInEditMode && !attribute._Including.HasFlag(Include.BuildPipeline))
                {
                    // Do not execute when building the player.
                    enableInEditMode = !BuildPipeline.isBuildingPlayer;
                }

                // Components that use the singleton pattern are candidates for not executing in the prefab stage
                // as a new instance will be created which could interfere with the scene stage instance.
                if (enableInEditMode && !attribute._Including.HasFlag(Include.PrefabStage))
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    _IsPrefabStageInstance = stage != null && gameObject.scene == stage.scene;

                    // Do not execute in prefab stage.
                    enableInEditMode = !_IsPrefabStageInstance;
                }

                // When singleton, destroy instance objects.
                if (enableInEditMode && attribute._Options.HasFlag(ExecuteDuringEditMode.Options.Singleton) &&
                    FindObjectsByType(GetType(), FindObjectsSortMode.None).Length > 1)
                {
                    enableInEditMode = false;
                    EditorApplication.update -= InternalDestroyNonSaveables;
                    EditorApplication.update += InternalDestroyNonSaveables;
                }

                // runInEditMode will immediately call Awake and OnEnable so we must not do this in OnValidate as there
                // are many restrictions which Unity will produce warnings for:
                // https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnValidate.html
                if (enableInEditMode)
                {
                    if (BuildPipeline.isBuildingPlayer)
                    {
                        // EditorApplication.update and Invoke are not called when building.
                        InternalEnableEditMode();
                    }
                    else
                    {
                        // Called between OnAwake/OnEnable and Start which makes it seamless.
                        EditorApplication.update -= InternalEnableEditMode;
                        EditorApplication.update += InternalEnableEditMode;
                    }
                }
            }

            _IsFirstOnValidate = false;
        }

        void InternalDestroyNonSaveables()
        {
            EditorApplication.update -= InternalDestroyNonSaveables;

            // See comment below.
            if (this == null) return;

            foreach (Transform transform in transform.GetComponentInChildren<Transform>(includeInactive: true))
            {
                if (transform.gameObject.hideFlags.HasFlag(HideFlags.DontSaveInEditor))
                {
                    Helpers.Destroy(transform.gameObject);
                }
            }
        }

        void InternalEnableEditMode()
        {
            EditorApplication.update -= InternalEnableEditMode;

            // If the scene that is being built is already opened then, there can be a rogue instance which registers
            // an event but is destroyed by the time it gets here. It has something to do with OnValidate being called
            // after the object is destroyed with _isFirstOnValidate being true.
            if (this == null) return;
            // Workaround to ExecuteAlways also executing during building which is often not what we want.
            runInEditMode = true;
        }

        static MethodInfo s_ExecuteValidators;
        [InitializeOnLoadMethod]
        static void Load()
        {
            var type = System.Type.GetType("WaveHarmonic.Crest.Editor.ValidatedHelper, WaveHarmonic.Crest.Shared.Editor");
            s_ExecuteValidators = type.GetMethod
            (
                "ExecuteValidators",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Object) },
                null
            );
        }
    }
}

#endif

namespace WaveHarmonic.Crest
{
    // Stores a reference to the owner so the GO can be deleted safely when duplicated/pasted.
    sealed class ManagedGameObject : MonoBehaviour
    {
        [field: SerializeField]
        public Component Owner { get; set; }
    }

    static class Extentions
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Manage(this Component owner, GameObject @object)
        {
            @object.AddComponent<ManagedGameObject>().Owner = owner;
        }
    }
}
