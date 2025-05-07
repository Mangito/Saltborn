// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    interface IReportsHeight
    {
        bool ReportHeight(ref Rect bounds, ref float minimum, ref float maximum);
    }

    interface IReportsDisplacement
    {
        bool ReportDisplacement(ref Rect bounds, ref float horizontal, ref float vertical);
    }

    /// <summary>
    /// Sets shader parameters for each geometry tile/chunk.
    /// </summary>
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    [@ExecuteDuringEditMode]
    sealed class WaterChunkRenderer : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        internal bool _DrawRenderBounds = false;

        static class ShaderIDs
        {
            public static readonly int s_ChunkMeshScaleAlpha = Shader.PropertyToID("_Crest_ChunkMeshScaleAlpha");
            public static readonly int s_ChunkGeometryGridWidth = Shader.PropertyToID("_Crest_ChunkGeometryGridWidth");
            public static readonly int s_ChunkFarNormalsWeight = Shader.PropertyToID("_Crest_ChunkFarNormalsWeight");
            public static readonly int s_ChunkNormalScrollSpeed = Shader.PropertyToID("_Crest_ChunkNormalScrollSpeed");
            public static readonly int s_ChunkMeshScaleAlphaSource = Shader.PropertyToID("_Crest_ChunkMeshScaleAlphaSource");
            public static readonly int s_ChunkGeometryGridWidthSource = Shader.PropertyToID("_Crest_ChunkGeometryGridWidthSource");
        }

        internal Bounds _BoundsLocal;
        Mesh _Mesh;
        public Renderer Rend { get; private set; }
        internal MaterialPropertyBlock _MaterialPropertyBlock;
        Matrix4x4 _PreviousObjectToWorld;

        internal Rect _UnexpandedBoundsXZ = new();
        public Rect UnexpandedBoundsXZ => _UnexpandedBoundsXZ;

        internal bool _Culled;
        internal bool _Visible;

        internal WaterRenderer _Water;

        public bool MaterialOverridden { get; set; }

        // We need to ensure that all water data has been bound for the mask to
        // render properly - this is something that needs to happen irrespective
        // of occlusion culling because we need the mask to render as a
        // contiguous surface.
        internal bool _WaterDataHasBeenBound = true;

        int _LodIndex = -1;

        public static List<IReportsHeight> HeightReporters { get; } = new();
        public static List<IReportsDisplacement> DisplacementReporters { get; } = new();

        private protected override System.Action<WaterRenderer> OnLateUpdateMethod => OnLateUpdate;

        private protected override void Initialize()
        {
            base.Initialize();

            _MaterialPropertyBlock ??= new();

            if (Rend == null)
            {
                Rend = GetComponent<Renderer>();
            }

            if (_Mesh == null)
            {
                // Meshes are cloned so it is safe to use sharedMesh in play mode. We need clones to modify the render bounds.
                _Mesh = GetComponent<MeshFilter>().sharedMesh;
            }
        }

        private protected override void OnStart()
        {
            base.OnStart();

            UpdateMeshBounds();
        }

        void OnLateUpdate(WaterRenderer water)
        {
            // Time slice update to distribute the load.
            if (!(transform.GetSiblingIndex() % water.TimeSliceBoundsUpdateFrameCount != Time.frameCount % water.Chunks.Count % water.TimeSliceBoundsUpdateFrameCount))
            {
                // This needs to be called on Update because the bounds depend on transform scale which can change. Also OnWillRenderObject depends on
                // the bounds being correct. This could however be called on scale change events, but would add slightly more complexity.
                UpdateMeshBounds();
            }

            // Update chunk shader data.
            _MaterialPropertyBlock ??= new();

            // FIXME: Sometimes thrown.
            // NullReferenceException: Object reference not set to an instance of an object
            // WaveHarmonic.Crest.WaterChunkRenderer.OnLateUpdate(WaveHarmonic.Crest.WaterRenderer water)(at Packages/com.waveharmonic.crest/Runtime/Scripts/Surface/WaterChunkRenderer.cs:119)
            // WaveHarmonic.Crest.WaterRenderer.LateUpdate()(at Packages/com.waveharmonic.crest/Runtime/Scripts/WaterRenderer.cs:733)
            if (Rend == null)
            {
                Rend = GetComponent<Renderer>();
            }

            Rend.GetPropertyBlock(_MaterialPropertyBlock);
            _MaterialPropertyBlock.SetInteger(Lod.ShaderIDs.s_LodIndex, _LodIndex);
            var data = water._PerCascadeInstanceData.Current[_LodIndex];
            _MaterialPropertyBlock.SetFloat(ShaderIDs.s_ChunkMeshScaleAlpha, data._MeshScaleLerp);
            _MaterialPropertyBlock.SetFloat(ShaderIDs.s_ChunkGeometryGridWidth, data._GeometryGridWidth);
            _MaterialPropertyBlock.SetFloat(ShaderIDs.s_ChunkFarNormalsWeight, data._FarNormalsWeight);
            _MaterialPropertyBlock.SetVector(ShaderIDs.s_ChunkNormalScrollSpeed, data._NormalScrollSpeeds);
            data = water._PerCascadeInstanceData.Previous(1)[_LodIndex];
            _MaterialPropertyBlock.SetFloat(ShaderIDs.s_ChunkMeshScaleAlphaSource, data._MeshScaleLerp);
            _MaterialPropertyBlock.SetFloat(ShaderIDs.s_ChunkGeometryGridWidthSource, data._GeometryGridWidth);
            Rend.SetPropertyBlock(_MaterialPropertyBlock);

#if UNITY_6000_0_OR_NEWER
            if (Application.isPlaying && RenderPipelineHelper.IsUniversal && water.WriteMotionVectors)
            {
                var material = water._MotionVectorsMaterial;

                var parameters = new RenderParams(material)
                {
                    motionVectorMode = MotionVectorGenerationMode.Object,
                    material = material,
                    matProps = _MaterialPropertyBlock,
                    worldBounds = Rend.bounds,
                    layer = water.Layer,
                    receiveShadows = false,
                    shadowCastingMode = ShadowCastingMode.Off,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                };

                if (_Mesh == null)
                {
                    _Mesh = GetComponent<MeshFilter>().sharedMesh;
                }

                Graphics.RenderMesh(parameters, _Mesh, 0, transform.localToWorldMatrix, _PreviousObjectToWorld);
                _PreviousObjectToWorld = transform.localToWorldMatrix;
            }
#endif
        }

        void UpdateMeshBounds()
        {
            UnityEngine.Profiling.Profiler.BeginSample("Crest.WaterChunkRenderer.UpdateMeshBounds");

            if (WaterBody.WaterBodies.Count > 0)
            {
                _UnexpandedBoundsXZ = ComputeBoundsXZ(transform, ref _BoundsLocal);
            }

            var newBounds = _BoundsLocal;
            ExpandBoundsForDisplacements(transform, ref newBounds);

            // FIXME: Fixes a crash which was reported twice. Could not reproduce.
            // NullReferenceException: Object reference not set to an instance of an object.
            // at WaveHarmonic.Crest.WaterChunkRenderer.UpdateMeshBounds()[0x00000] in < 00000000000000000000000000000000 >:0
            // at WaveHarmonic.Crest.WaterChunkRenderer.OnUpdate(WaveHarmonic.Crest.WaterRenderer water)[0x00000] in < 00000000000000000000000000000000 >:0
            // at WaveHarmonic.Crest.WaterRenderer.Update()[0x00000] in < 00000000000000000000000000000000 >:0
            if (_Mesh == null)
            {
                _Mesh = GetComponent<MeshFilter>().sharedMesh;
            }

            _Mesh.bounds = newBounds;

            UnityEngine.Profiling.Profiler.EndSample();
        }

        public static Rect ComputeBoundsXZ(Transform transform, ref Bounds bounds)
        {
            // Since chunks are axis-aligned it is safe to rotate the bounds.
            var center = transform.rotation * bounds.center * transform.lossyScale.x + transform.position;
            var size = transform.rotation * bounds.size * transform.lossyScale.x;
            // Rotation can make size negative.
            return new(0, 0, Mathf.Abs(size.x), Mathf.Abs(size.z))
            {
                center = center.XZ(),
            };
        }

        static Camera s_CurrentCamera = null;

        static void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Camera.current is only supported in the built-in pipeline. This provides the current camera for
            // OnWillRenderObject for SRPs. BeginCameraRendering is called for each active camera in every frame.
            // OnWillRenderObject is called after BeginCameraRendering for the current camera so this works.
            s_CurrentCamera = camera;
        }

        // Used by the water mask system if we need to render the water mask in situations
        // where the water itself doesn't need to be rendered or has otherwise been disabled
        internal void Bind(Camera camera)
        {
            _WaterDataHasBeenBound = true;

            if (Rend == null)
            {
                return;
            }

            if (!MaterialOverridden && Rend.sharedMaterial != _Water.Material)
            {
                Rend.sharedMaterial = _Water.Material;
            }
        }

        void OnDestroy()
        {
            Helpers.Destroy(_Mesh);
        }

        // Called when visible to a camera
        void OnWillRenderObject()
        {
            // Camera.current is only supported in built-in pipeline.
            if (RenderPipelineHelper.IsLegacy && Camera.current != null)
            {
                s_CurrentCamera = Camera.current;
            }

            // If only the game view is visible, this reference will be dropped for SRP on recompile.
            if (s_CurrentCamera == null)
            {
                return;
            }

            // Depth texture is used by water shader for transparency/depth fog, and for fading out foam at shoreline.
            s_CurrentCamera.depthTextureMode |= DepthTextureMode.Depth;

            Bind(s_CurrentCamera);

            if (_DrawRenderBounds)
            {
                Rend.bounds.DebugDraw();
            }
        }

        // this is called every frame because the bounds are given in world space and depend on the transform scale, which
        // can change depending on view altitude
        public void ExpandBoundsForDisplacements(Transform transform, ref Bounds bounds)
        {
            var scale = transform.lossyScale;
            var rotation = transform.rotation;

            var boundsPadding = _Water.MaximumHorizontalDisplacement;
            var expandXZ = boundsPadding / scale.x;
            var boundsY = _Water.MaximumVerticalDisplacement;

            // Extend the kinematic bounds slightly to give room for dynamic waves.
            if (_Water._DynamicWavesLod.Enabled)
            {
                boundsY += 5f;
            }

            var extents = bounds.extents;

            // Extend bounds by global waves.
            bounds.extents = new(extents.x + expandXZ, boundsY, extents.z + expandXZ);

            extents = bounds.extents;
            var center = bounds.center;
            var size = bounds.size;

            // Get XZ bounds. Doing this manually bypasses updating render bounds call.
            Rect rect;
            {
                var p1 = transform.position;
                var p2 = rotation * new Vector3(center.x, 0f, center.z);
                var s1 = scale;
                var s2 = rotation * new Vector3(size.x, 0f, size.z);

                rect = new(0, 0, Mathf.Abs(s1.x * s2.x), Mathf.Abs(s1.z * s2.z))
                {
                    center = new(p1.x + p2.x, p1.z + p2.z)
                };
            }

            // Extend bounds by local waves.
            {
                var totalHorizontal = 0f;
                var totalVertical = 0f;

                foreach (var reporter in DisplacementReporters)
                {
                    var horizontal = 0f;
                    var vertical = 0f;
                    if (reporter.ReportDisplacement(ref rect, ref horizontal, ref vertical))
                    {
                        totalHorizontal += horizontal;
                        totalVertical += vertical;
                    }
                }

                boundsPadding = totalHorizontal;
                expandXZ = boundsPadding / scale.x;
                boundsY = totalVertical;

                bounds.extents = new(extents.x + expandXZ, extents.y + boundsY, extents.z + expandXZ);
            }

            // Expand and offset bounds by height.
            {
                var minimumWaterLevelBounds = 0f;
                var maximumWaterLevelBounds = 0f;

                foreach (var reporter in HeightReporters)
                {
                    var minimum = 0f;
                    var maximum = 0f;
                    if (reporter.ReportHeight(ref rect, ref minimum, ref maximum))
                    {
                        minimumWaterLevelBounds = Mathf.Max(minimumWaterLevelBounds, Mathf.Abs(Mathf.Min(minimum, _Water.SeaLevel) - _Water.SeaLevel));
                        maximumWaterLevelBounds = Mathf.Max(maximumWaterLevelBounds, Mathf.Abs(Mathf.Max(maximum, _Water.SeaLevel) - _Water.SeaLevel));
                    }
                }

                minimumWaterLevelBounds *= 0.5f;
                maximumWaterLevelBounds *= 0.5f;

                boundsY = minimumWaterLevelBounds + maximumWaterLevelBounds;
                extents = bounds.extents;
                bounds.extents = new(extents.x, extents.y + boundsY, extents.z);

                var offset = maximumWaterLevelBounds - minimumWaterLevelBounds;
                center = bounds.center;
                bounds.center = new(center.x, center.y + offset, center.z);
            }
        }

        public void SetInstanceData(int lodIndex)
        {
            _LodIndex = lodIndex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStatics()
        {
            // Init here from 2019.3 onwards
            s_CurrentCamera = null;
            HeightReporters.Clear();
            DisplacementReporters.Clear();
        }

        [RuntimeInitializeOnLoadMethod]
        static void RunOnStart()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        }
    }

    static class BoundsHelper
    {
        internal static void DebugDraw(this Bounds b)
        {
            var xmin = b.min.x;
            var ymin = b.min.y;
            var zmin = b.min.z;
            var xmax = b.max.x;
            var ymax = b.max.y;
            var zmax = b.max.z;

            Debug.DrawLine(new(xmin, ymin, zmin), new(xmin, ymin, zmax));
            Debug.DrawLine(new(xmin, ymin, zmin), new(xmax, ymin, zmin));
            Debug.DrawLine(new(xmax, ymin, zmax), new(xmin, ymin, zmax));
            Debug.DrawLine(new(xmax, ymin, zmax), new(xmax, ymin, zmin));

            Debug.DrawLine(new(xmin, ymax, zmin), new(xmin, ymax, zmax));
            Debug.DrawLine(new(xmin, ymax, zmin), new(xmax, ymax, zmin));
            Debug.DrawLine(new(xmax, ymax, zmax), new(xmin, ymax, zmax));
            Debug.DrawLine(new(xmax, ymax, zmax), new(xmax, ymax, zmin));

            Debug.DrawLine(new(xmax, ymax, zmax), new(xmax, ymin, zmax));
            Debug.DrawLine(new(xmin, ymin, zmin), new(xmin, ymax, zmin));
            Debug.DrawLine(new(xmax, ymin, zmin), new(xmax, ymax, zmin));
            Debug.DrawLine(new(xmin, ymax, zmax), new(xmin, ymin, zmax));
        }
    }
}
