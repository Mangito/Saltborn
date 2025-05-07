// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Data storage for for the Renderer input mode.
    /// </summary>
    public abstract partial class RendererLodInputData : LodInputData
    {
        [Tooltip("The renderer to use for this input.\n\nCan be anything that inherits from <i>Renderer</i> like <i>MeshRenderer</i>, <i>TrailRenderer</i> etc.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal Renderer _Renderer;

        [Tooltip("Forces the renderer to only render into the LOD data, and not to render in the scene as it normally would.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _DisableRenderer = true;

        [Tooltip("Whether to set the shader pass manually.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _OverrideShaderPass;

        [Tooltip("The shader pass to execute.\n\nSet to -1 to execute all passes.")]
        [@Predicated(nameof(_OverrideShaderPass))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal int _ShaderPassIndex;

#pragma warning disable 414
        [Tooltip("Check that the shader applied to this object matches the input type.\n\nFor example, an Animated Waves input object has an Animated Waves input shader.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _CheckShaderName = true;

        [Tooltip("Check that the shader applied to this object has only a single pass, as only the first pass is executed for most inputs.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _CheckShaderPasses = true;
#pragma warning restore 414


        static class ShaderIDs
        {
            public static readonly int s_Time = Shader.PropertyToID("_Time");
        }


        // Some renderers require multiple materials like particles with trails.
        // We pass this to GetSharedMaterials to avoid allocations.
        internal List<Material> _Materials = new();
        MaterialPropertyBlock _MaterialPropertyBlock;

        internal abstract string ShaderPrefix { get; }

        internal override bool IsEnabled => _Renderer != null && _MaterialPropertyBlock != null;

        internal override void RecalculateRect()
        {
            _Rect = Rect.MinMaxRect(_Renderer.bounds.min.x, _Renderer.bounds.min.z, _Renderer.bounds.max.x, _Renderer.bounds.max.z);
        }

        internal override void RecalculateBounds()
        {
            _Bounds = _Renderer.bounds;
        }

        bool AnyOtherInputsControllingRenderer(Renderer renderer)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var rootGameObject in scene.GetRootGameObjects())
                {
                    foreach (var component in rootGameObject.GetComponentsInChildren<LodInput>())
                    {
                        if (component == _Input)
                        {
                            continue;
                        }

                        if (component.Data is not RendererLodInputData data)
                        {
                            continue;
                        }

                        if (component.isActiveAndEnabled && data._DisableRenderer && data._Renderer == renderer)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal override void OnEnable()
        {
            _MaterialPropertyBlock ??= new();

            if (_Renderer == null)
            {
                return;
            }

            _Renderer.GetSharedMaterials(_Materials);

            if (_DisableRenderer)
            {
                // If we disable using "enabled" then the renderer might not behave correctly (eg line/trail positions
                // will not be updated). This keeps the scripting side of the component running and just disables the
                // rendering. Similar to disabling the Renderer module on the Particle System. It also is not serialized.
                _Renderer.forceRenderingOff = true;
            }
        }

        internal override void OnDisable()
        {
            if (_Renderer != null && _DisableRenderer && !AnyOtherInputsControllingRenderer(_Renderer))
            {
                _Renderer.forceRenderingOff = false;
            }
        }

        internal override void OnUpdate()
        {
            if (_Renderer == null)
            {
                return;
            }

            // We have to check this every time as the user could change the materials and it is too difficult to track.
            // Doing this in LateUpdate could add one frame latency to receiving the change.
            _Renderer.GetSharedMaterials(_Materials);

            // Always recalculate, as there are too much to track.
            _RecalculateBounds = true;
            _RecalculateRect = _Bounds != _Renderer.bounds;
        }

        internal override void Draw(Lod lod, Component component, CommandBuffer buffer, RenderTargetIdentifier target, int slice)
        {
            // NOTE: Inputs will only change the first material (only ShapeWaves at the moment).

            for (var i = 0; i < _Materials.Count; i++)
            {
                var material = _Materials[i];
                Debug.AssertFormat(material != null, _Renderer, "Crest: Attached renderer has an empty material slot which is not allowed.");

#if UNITY_EDITOR
                // Empty material slots is a user error, but skip so we do not spam errors.
                if (material == null)
                {
                    continue;
                }
#endif

                var pass = _ShaderPassIndex;
                if (ShapeWaves.s_RenderPassOverride > -1)
                {
                    // Needs to use a second pass to disable blending.
                    pass = ShapeWaves.s_RenderPassOverride;
                }
                else if (!_OverrideShaderPass)
                {
                    // BIRP/URP SG first pass is the right one.
                    pass = 0;

                    // Support HDRP SG. It will always have more than one pass.
                    if (RenderPipelineHelper.IsHighDefinition && material.shader.passCount > 1)
                    {
                        var sgPass = material.FindPass("ForwardOnly");
                        if (sgPass > -1) pass = sgPass;
                    }
                }
                else if (_ShaderPassIndex > material.shader.passCount - 1)
                {
                    return;
                }

                // Time is not set for us for some reason… Use Time.timeSinceLevelLoad as per:
                // https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
                if (RenderPipelineHelper.IsLegacy)
                {
                    _Renderer.GetPropertyBlock(_MaterialPropertyBlock);
                    _MaterialPropertyBlock.SetVector(ShaderIDs.s_Time, new
                    (
                        Time.timeSinceLevelLoad / 20,
                        Time.timeSinceLevelLoad,
                        Time.timeSinceLevelLoad * 2f,
                        Time.timeSinceLevelLoad * 3f
                    ));
                    _Renderer.SetPropertyBlock(_MaterialPropertyBlock);
                }

                // By default, shaderPass is -1 which is all passes. Shader Graph will produce multi-pass shaders
                // for depth etc so we should only render one pass. Unlit SG will have the unlit pass first.
                // Submesh count generally must equal number of materials.
                buffer.DrawRenderer(_Renderer, material, submeshIndex: i, pass);
            }
        }

        void SetRenderer(Renderer previous, Renderer current)
        {
            if (previous == current) return;
            if (_Input == null || !_Input.isActiveAndEnabled) return;

            if (previous != null && _DisableRenderer && !AnyOtherInputsControllingRenderer(previous))
            {
                // Turn off if there are no other inputs have set this value.
                previous.forceRenderingOff = false;
            }

            if (current != null)
            {
                current.forceRenderingOff = true;
            }
        }

        void SetDisableRenderer(bool previous, bool current)
        {
            if (previous == current) return;
            if (_Input == null || !_Input.isActiveAndEnabled) return;

            if (_Renderer != null && !AnyOtherInputsControllingRenderer(_Renderer))
            {
                _Renderer.forceRenderingOff = _DisableRenderer;
            }
        }

#if UNITY_EDITOR
        [@OnChange]
        internal override void OnChange(string propertyPath, object previousValue)
        {
            switch (propertyPath)
            {
                case nameof(_Renderer):
                    SetRenderer((Renderer)previousValue, _Renderer);
                    break;
                case nameof(_DisableRenderer):
                    SetDisableRenderer((bool)previousValue, _DisableRenderer);
                    break;
            }
        }

        internal override bool InferMode(Component component, ref LodInputMode mode)
        {
            if (component.TryGetComponent(out _Renderer))
            {
                mode = LodInputMode.Renderer;
                return true;
            }

            return false;
        }
#endif
    }
}
