// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

#if !UNITY_2023_2_OR_NEWER
using GraphicsFormatUsage = UnityEngine.Experimental.Rendering.FormatUsage;
#endif

namespace WaveHarmonic.Crest
{
    using Inputs = SortedList<int, ILodInput>;

    /// <summary>
    /// Texture format preset.
    /// </summary>
    public enum LodTextureFormatMode
    {
        /// <summary>
        /// Uses <see cref="Lod.TextureFormat"/>.
        /// </summary>
        Manual,

        /// <summary>
        /// Chooses a texture format for performance.
        /// </summary>
        Performance = 100,

        /// <summary>
        /// Chooses a texture format for precision.
        /// </summary>
        /// <remarks>
        /// This format can reduce artifacts.
        /// </remarks>
        Precision = 200,

        /// <summary>
        /// Chooses a texture format based on another.
        /// </summary>
        /// <remarks>
        /// For example, Dynamic Waves will match precision of Animated Waves.
        /// </remarks>
        Automatic = 300,
    }

    /// <summary>
    /// Base class for data/behaviours created on each LOD.
    /// </summary>
    [System.Serializable]
    public abstract partial class Lod
    {
        [Tooltip("Whether the simulation is enabled.")]
        [@GenerateAPI(Getter.Custom, Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _Enabled;

        [Tooltip("Whether to override the resolution.\n\nIf not enabled, then the simulation will use the resolution defined on the Water Renderer.")]
        [@Predicated(typeof(AnimatedWavesLod), inverted: true, hide: true)]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField, SerializeField]
        internal bool _OverrideResolution = true;

        [Tooltip("The resolution of the simulation data.\n\nSet higher for sharper results at the cost of higher memory usage.")]
        [@Predicated(typeof(AnimatedWavesLod), inverted: true, hide: true)]
        [@Delayed]
        [@GenerateAPI(Getter.Custom, Setter.Dirty)]
        [SerializeField]
        internal int _Resolution = 256;

        [Tooltip("Chooses a texture format based on a preset value.")]
        [@Filtered]
        [@GenerateAPI(Setter.Dirty)]
        [SerializeField]
        private protected LodTextureFormatMode _TextureFormatMode = LodTextureFormatMode.Performance;

        [Tooltip("The render texture format used for this simulation data.\n\nIt will be overriden if the format is incompatible with the platform.")]
        [@ShowComputedProperty(nameof(RequestedTextureFormat))]
        [@Predicated(nameof(_TextureFormatMode), inverted: true, nameof(LodTextureFormatMode.Manual), hide: true)]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField, SerializeField]
        internal GraphicsFormat _TextureFormat;

        // NOTE: This MUST match the value in Constants.hlsl, as it
        // determines the size of the texture arrays in the shaders.
        internal const int k_MaximumSlices = 15;

        // NOTE: these MUST match the values in Constants.hlsl
        // 64 recommended as a good common minimum: https://www.reddit.com/r/GraphicsProgramming/comments/aeyfkh/for_compute_shaders_is_there_an_ideal_numthreads/
        internal const int k_ThreadGroupSize = 8;
        internal const int k_ThreadGroupSizeX = k_ThreadGroupSize;
        internal const int k_ThreadGroupSizeY = k_ThreadGroupSize;

        internal static class ShaderIDs
        {
            public static readonly int s_LodIndex = Shader.PropertyToID("_Crest_LodIndex");
            public static readonly int s_LodChange = Shader.PropertyToID("_Crest_LodChange");
        }

        // Used for creating shader property names etc.
        internal abstract string ID { get; }
        internal virtual string Name => ID;

        /// <summary>
        /// The requested texture format used for this simulation, either by manual mode or
        /// one of the aliases. It will be overriden if the format is incompatible with the
        /// platform (<see cref="CompatibleTextureFormat"/>).
        /// </summary>
        private protected abstract GraphicsFormat RequestedTextureFormat { get; }

        /// <summary>
        /// This is the platform compatible texture format that will used.
        /// </summary>
        public GraphicsFormat CompatibleTextureFormat { get; private set; }

        private protected abstract Color ClearColor { get; }
        private protected abstract bool NeedToReadWriteTextureData { get; }
        private protected abstract Inputs Inputs { get; }
        internal abstract Color GizmoColor { get; }
        internal virtual int BufferCount => 1;
        private protected virtual Texture2DArray NullTexture => BlackTextureArray;
        private protected virtual bool RequiresClearBorder => false;

        private protected IQueryable Queryable { get; set; }

        // This is used as alternative to Texture2D.blackTexture, as using that
        // is not possible in some shaders.
        static Texture2DArray s_BlackTextureArray = null;
        static Texture2DArray BlackTextureArray
        {
            get
            {
                if (s_BlackTextureArray == null)
                {
                    s_BlackTextureArray = TextureArrayHelpers.CreateTexture2DArray(Texture2D.blackTexture, k_MaximumSlices);
                    s_BlackTextureArray.name = "_Crest_LodBlackTexture";
                }

                return s_BlackTextureArray;
            }
        }

        static readonly GraphicsFormatUsage s_GraphicsFormatUsage =
            // Ensures a non compressed format is returned.
            GraphicsFormatUsage.LoadStore |
            // All these textures are sampled at some point.
            GraphicsFormatUsage.Sample |
            // Always use linear filtering.
            GraphicsFormatUsage.Linear;

        internal virtual bool RunsInHeadless => false;

        private protected BufferedData<RenderTexture> _Targets;
        internal RenderTexture DataTexture => _Targets.Current;
        internal RenderTexture GetDataTexture(int frameDelta) => _Targets.Previous(frameDelta);

        private protected Matrix4x4[] _ViewMatrices = new Matrix4x4[k_MaximumSlices];
        private protected Cascade[] _Cascades = new Cascade[k_MaximumSlices];
        internal Cascade[] Cascades => _Cascades;
        private protected BufferedData<Vector4[]> _SamplingParameters;

        internal int Slices => _Water.LodLevels;

        // Currently use as a failure flag.
        private protected bool _Valid;

        internal WaterRenderer _Water;
        internal WaterRenderer Water => _Water;

        private protected int _TargetsToClear;

        private protected readonly int _TextureShaderID;
        private protected readonly int _TextureSourceShaderID;
        private protected readonly int _SamplingParametersShaderID;
        private protected readonly int _SamplingParametersCascadeShaderID;
        private protected readonly int _SamplingParametersCascadeSourceShaderID;

        readonly string _TextureName;

        internal Lod()
        {
            // @Garbage
            var name = $"g_Crest_Cascade{ID}";
            _TextureShaderID = Shader.PropertyToID(name);
            _TextureSourceShaderID = Shader.PropertyToID($"{name}Source");
            _SamplingParametersShaderID = Shader.PropertyToID($"g_Crest_SamplingParameters{ID}");
            _SamplingParametersCascadeShaderID = Shader.PropertyToID($"g_Crest_SamplingParametersCascade{ID}");
            _SamplingParametersCascadeSourceShaderID = Shader.PropertyToID($"g_Crest_SamplingParametersCascade{ID}Source");

            _TextureName = $"_Crest_{ID}Lod";
        }

        private protected RenderTexture CreateLodDataTextures()
        {
            RenderTexture result = new(Resolution, Resolution, 0, CompatibleTextureFormat)
            {
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                useMipMap = false,
                name = _TextureName,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = Slices,
                enableRandomWrite = NeedToReadWriteTextureData
            };
            result.Create();
            return result;
        }

        private protected void FlipBuffers()
        {
            if (_ReAllocateTexture)
            {
                ReAllocate();
            }

            _Targets.Flip();
            _SamplingParameters.Flip();

            UpdateSamplingParameters();
        }

        private protected void Clear(RenderTexture target)
        {
            Helpers.ClearRenderTexture(target, ClearColor, depth: false);
        }

        /// <summary>
        /// Clears persistent LOD data. Some simulations have persistent data which can linger for a little while after
        /// being disabled. This will manually clear that data.
        /// </summary>
        internal virtual void ClearLodData()
        {
            // Empty.
        }

        private protected virtual bool AlwaysClear => false;

        // Only works with input-only data (ie no simulation steps).
        internal virtual void BuildCommandBuffer(WaterRenderer water, CommandBuffer buffer)
        {
            FlipBuffers();

            buffer.BeginSample(Name);

            if (_TargetsToClear > 0 || AlwaysClear)
            {
                buffer.SetRenderTarget(DataTexture, 0, CubemapFace.Unknown, -1);
                buffer.ClearRenderTarget(RTClearFlags.Color, ClearColor, 0, 0);

                _TargetsToClear--;
            }

            if (Inputs.Count > 0)
            {
                SubmitDraws(buffer, Inputs, DataTexture);

                // Ensure all targets clear when there are no inputs.
                _TargetsToClear = _Targets.Size;
            }

            if (RequiresClearBorder)
            {
                ClearBorder(buffer);
            }

            Queryable?.UpdateQueries(_Water);

            buffer.EndSample(Name);
        }

        private protected bool SubmitDraws(CommandBuffer buffer, Inputs draws, RenderTargetIdentifier target, int pass = -1, bool filter = false)
        {
            var drawn = false;

            foreach (var draw in draws)
            {
                var input = draw.Value;
                if (!input.Enabled)
                {
                    continue;
                }

                if (pass != -1)
                {
                    var p = input.Pass;
                    if (p != -1 && p != pass) continue;
                }

                var rect = input.Rect;

                if (input.IsCompute)
                {
                    var smallest = 0;
                    if (rect != Rect.zero)
                    {
                        smallest = -1;
                        for (var slice = Slices - 1; slice >= 0; slice--)
                        {
                            if (rect != Rect.zero && !rect.Overlaps(Cascades[slice].TexelRect)) break;
                            smallest = slice;
                        }

                        if (smallest < 0) continue;
                    }

                    // Pass the slice count to only draw to valid slices.
                    input.Draw(this, buffer, target, pass, slice: Slices - smallest);
                    drawn = true;
                    continue;
                }

                for (var slice = Slices - 1; slice >= 0; slice--)
                {
                    if (rect != Rect.zero && !rect.Overlaps(Cascades[slice].TexelRect)) break;

                    var weight = filter ? input.Filter(_Water, slice) : 1f;
                    if (weight <= 0f) continue;

                    // Parameters override RTI values:
                    // https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.SetRenderTarget.html
                    buffer.SetRenderTarget(target, 0, CubemapFace.Unknown, slice);
                    buffer.SetGlobalInt(ShaderIDs.s_LodIndex, slice);

                    // This will work for CG but not for HDRP hlsl files.
                    buffer.SetViewProjectionMatrices(_ViewMatrices[slice], _Water.GetProjectionMatrix(slice));

                    input.Draw(this, buffer, target, pass, weight, slice);
                    drawn = true;
                }
            }

            return drawn;
        }

        /// <summary>
        /// Set a new origin. This is equivalent to subtracting the new origin position from any world position state.
        /// </summary>
        internal void SetOrigin(Vector3 newOrigin)
        {
            _SamplingParameters.RunLambda(data =>
            {
                for (var index = 0; index < _Water.LodLevels; index++)
                {
                    // We really only care about the previous states, as the current/next frame will be
                    // re-calculated. This realigns the snapped position with the now shifted camera.
                    data[index].x -= newOrigin.x;
                    data[index].y -= newOrigin.z;
                }
            });
        }

        void ClearBorder(CommandBuffer buffer)
        {
            var size = Resolution / 8;

            var wrapper = new PropertyWrapperCompute(buffer, WaterResources.Instance.Compute._Clear, 1);
            wrapper.SetTexture(Crest.ShaderIDs.s_Target, DataTexture);
            wrapper.SetVector(Crest.ShaderIDs.s_ClearColor, ClearColor);
            wrapper.SetInteger(Crest.ShaderIDs.s_Resolution, Resolution);
            wrapper.SetInteger(Crest.ShaderIDs.s_TargetSlice, Slices - 1);
            wrapper.Dispatch(size, 1, 1);

            wrapper = new PropertyWrapperCompute(buffer, WaterResources.Instance.Compute._Clear, 2);
            wrapper.SetTexture(Crest.ShaderIDs.s_Target, DataTexture);
            wrapper.SetVector(Crest.ShaderIDs.s_ClearColor, ClearColor);
            wrapper.SetInteger(Crest.ShaderIDs.s_Resolution, Resolution);
            wrapper.SetInteger(Crest.ShaderIDs.s_TargetSlice, Slices - 1);
            wrapper.Dispatch(1, size, 1);
        }

        void UpdateSamplingParameters()
        {
            for (var slice = 0; slice < Slices; slice++)
            {
                // Find snap period.
                var texel = 2f * 2f * _Water.CalcLodScale(slice) / Resolution;
                // Snap so that shape texels are stationary.
                var snapped = _Water.Root.position - new Vector3(Mathf.Repeat(_Water.Root.position.x, texel), 0, Mathf.Repeat(_Water.Root.position.z, texel));

                var cascade = new Cascade(snapped.XZ(), texel, Resolution);
                _Cascades[slice] = cascade;
                _SamplingParameters.Current[slice] = cascade.Packed;

                _ViewMatrices[slice] = WaterRenderer.CalculateViewMatrixFromSnappedPositionRHS(snapped);
            }

            Shader.SetGlobalVector(_SamplingParametersShaderID, new(_Water.LodLevels, Resolution, 1f / Resolution, 0));
            Shader.SetGlobalVectorArray(_SamplingParametersCascadeShaderID, _SamplingParameters.Current);

            if (BufferCount > 1)
            {
                Shader.SetGlobalVectorArray(_SamplingParametersCascadeSourceShaderID, _SamplingParameters.Previous(1));
            }
        }

        /// <summary>
        /// Returns index of lod that completely covers the sample area. If no such lod
        /// available, returns -1.
        /// </summary>
        internal int SuggestIndex(Rect sampleArea)
        {
            for (var slice = 0; slice < Slices; slice++)
            {
                var cascade = _Cascades[slice];

                // Shape texture needs to completely contain sample area.
                var rect = cascade.TexelRect;

                // Shrink rect by 1 texel border - this is to make finite differences fit as well.
                var texel = cascade._Texel;
                rect.x += texel; rect.y += texel;
                rect.width -= 2f * texel; rect.height -= 2f * texel;

                if (!rect.Contains(sampleArea.min) || !rect.Contains(sampleArea.max))
                {
                    continue;
                }

                return slice;
            }

            return -1;
        }

        /// <summary>
        /// Returns index of lod that completely covers the sample area, and contains
        /// wavelengths that repeat no more than twice across the smaller spatial length. If
        /// no such lod available, returns -1. This means high frequency wavelengths are
        /// filtered out, and the lod index can be used for each sample in the sample area.
        /// </summary>
        internal int SuggestIndexForWaves(Rect sampleArea)
        {
            return SuggestIndexForWaves(sampleArea, Mathf.Min(sampleArea.width, sampleArea.height));
        }

        internal int SuggestIndexForWaves(Rect sampleArea, float minimumSpatialLength)
        {
            var count = Slices;

            for (var index = 0; index < count; index++)
            {
                var cascade = _Cascades[index];

                // Shape texture needs to completely contain sample area.
                var rect = cascade.TexelRect;

                // Shrink rect by 1 texel border - this is to make finite differences fit as well.
                var texel = cascade._Texel;
                rect.x += texel; rect.y += texel;
                rect.width -= 2f * texel; rect.height -= 2f * texel;

                if (!rect.Contains(sampleArea.min) || !rect.Contains(sampleArea.max))
                {
                    continue;
                }

                // The smallest wavelengths should repeat no more than twice across the smaller
                // spatial length. Unless we're in the last LOD - then this is the best we can do.
                var minimumWavelength = _Water.MaximumWavelength(index) / 2f;
                if (minimumWavelength < minimumSpatialLength / 2f && index < count - 1)
                {
                    continue;
                }

                return index;
            }

            return -1;
        }

        /// <summary>
        /// Bind data needed to load or compute from this simulation.
        /// </summary>
        internal virtual void Bind<T>(T target) where T : IPropertyWrapper
        {

        }

        internal virtual void Initialize()
        {
            // All simulations require a GPU so do not proceed any further.
            if (_Water.IsRunningWithoutGraphics)
            {
                _Valid = false;
                return;
            }

            // Some simulations are pointless in non-interactive mode.
            if (_Water.IsRunningHeadless && !RunsInHeadless)
            {
                _Valid = false;
                return;
            }

            // Validate textures.
            {
                // Find a compatible texture format.
                CompatibleTextureFormat = Helpers.GetCompatibleTextureFormat(RequestedTextureFormat, s_GraphicsFormatUsage, Name, NeedToReadWriteTextureData);

                if (CompatibleTextureFormat == GraphicsFormat.None)
                {
                    Debug.Log($"Crest: Disabling {Name} simulation due to no valid available texture format.");
                    _Valid = false;
                    return;
                }

                Debug.Assert(Slices <= k_MaximumSlices);
            }

            _Valid = true;

            Allocate();
        }

        internal virtual void SetGlobals(bool enable)
        {
            if (_Water.IsRunningWithoutGraphics) return;
            // Bind/unbind data texture for all shaders.
            Shader.SetGlobalTexture(_TextureShaderID, enable && Enabled ? DataTexture : NullTexture);
        }

        internal virtual void Enable()
        {
            // Blank
        }

        internal virtual void Disable()
        {
            // Always clean up provider (CPU may be running).
            Queryable?.CleanUp();
        }

        internal virtual void Destroy()
        {
            // Release resources and destroy object to avoid reference leak.
            _Targets?.RunLambda(x =>
            {
                if (x != null) x.Release();
                Helpers.Destroy(x);
            });
        }

        private protected virtual void Allocate()
        {
            _Targets = new(BufferCount, CreateLodDataTextures);
            _Targets.RunLambda(Clear);

            _SamplingParameters = new(BufferCount, () => new Vector4[k_MaximumSlices]);

            // Bind globally once here on init, which will bind to all graphics shaders (not compute)
            Shader.SetGlobalTexture(_TextureShaderID, DataTexture);

            _ReAllocateTexture = false;
        }

        bool GetEnabled() => _Enabled && _Valid;

        // NOTE: This could be called by the user due to API.
        void SetEnabled(bool previous, bool current)
        {
            if (previous == current) return;
            if (_Water == null || !_Water.isActiveAndEnabled) return;

            if (current)
            {
                Initialize();
                Enable();
            }
            else
            {
                Disable();
                Destroy();
            }

            SetGlobals(current);
        }

        int GetResolution() => _OverrideResolution ? _Resolution : Water.LodResolution;

        private protected void ReAllocate()
        {
            if (!Enabled) return;
            CompatibleTextureFormat = Helpers.GetCompatibleTextureFormat(RequestedTextureFormat, s_GraphicsFormatUsage, Name, NeedToReadWriteTextureData);
            var descriptor = _Targets.Current.descriptor;
            descriptor.height = descriptor.width = Resolution;
            descriptor.graphicsFormat = CompatibleTextureFormat;
            _Targets.RunLambda(texture =>
            {
                texture.Release();
                texture.descriptor = descriptor;
                texture.Create();
            });

            _ReAllocateTexture = false;
        }

#if UNITY_EDITOR
        [@OnChange]
        private protected virtual void OnChange(string propertyPath, object previousValue)
        {
            switch (propertyPath)
            {
                case nameof(_Enabled):
                    SetEnabled((bool)previousValue, _Enabled);
                    break;
                case nameof(_Resolution):
                case nameof(_OverrideResolution):
                case nameof(_TextureFormat):
                case nameof(_TextureFormatMode):
                    ReAllocate();
                    break;
            }
        }
#endif
    }

    // API
    partial class Lod
    {
        bool _ReAllocateTexture;

        void SetDirty<I>(I previous, I current) where I : System.IEquatable<I>
        {
            if (Equals(previous, current)) return;
            _ReAllocateTexture = true;
        }

        void SetDirty(System.Enum previous, System.Enum current)
        {
            if (previous == current) return;
            _ReAllocateTexture = true;
        }
    }

    /// <summary>
    /// Base type for simulations with a provider.
    /// </summary>
    /// <typeparam name="T">The query provider.</typeparam>
    public abstract class Lod<T> : Lod where T : IQueryProvider
    {
        /// <summary>
        /// Provides data from the GPU to CPU.
        /// </summary>
        public T Provider { get; set; }
        private protected abstract T CreateProvider(bool enable);

        internal override void SetGlobals(bool enable)
        {
            base.SetGlobals(enable);
            // We should always have a provider (null provider if disabled).
            InitializeProvider(enable);
        }

        private protected void InitializeProvider(bool enable)
        {
            Provider = CreateProvider(enable);
            // None providers are not IQueryable.
            Queryable = Provider as IQueryable;
        }
    }
}
