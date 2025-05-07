// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;
using WaveHarmonic.Crest.Internal;

#if !UNITY_2023_2_OR_NEWER
using GraphicsFormatUsage = UnityEngine.Experimental.Rendering.FormatUsage;
#endif

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// General purpose helpers which, at the moment, do not warrant a seperate file.
    /// </summary>
    static partial class Helpers
    {
        public static class ShaderIDs
        {
            public static readonly int s_MainTexture = Shader.PropertyToID("_Utility_MainTexture");
        }

        static Mesh s_Plane;

        /// <summary>
        /// Plane geometry
        /// </summary>
        public static Mesh PlaneMesh
        {
            get
            {
                if (s_Plane) return s_Plane;
                return s_Plane = Resources.GetBuiltinResource<Mesh>("New-Plane.fbx");
            }
        }

        static Mesh s_Quad;

        /// <summary>
        /// Quad geometry
        /// </summary>
        public static Mesh QuadMesh
        {
            get
            {
                if (s_Quad) return s_Quad;
                return s_Quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            }
        }

        static Mesh s_SphereMesh;

        /// <summary>
        /// Sphere geometry
        /// </summary>
        public static Mesh SphereMesh
        {
            get
            {
                if (s_SphereMesh) return s_SphereMesh;
                return s_SphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            }
        }

        internal static int SiblingIndexComparison(int x, int y) => x.CompareTo(y);

        /// <summary>
        /// Comparer that always returns less or greater, never equal, to get work around unique key constraint
        /// </summary>
        internal static int DuplicateComparison(int x, int y)
        {
            var result = x.CompareTo(y);
            // If non-zero, use result, otherwise return greater (never equal)
            return result != 0 ? result : 1;
        }

        static Vector2Int CalculateResolution(Vector2 resolution, int maximum)
        {
            // Enforce maximum and scale to maintain aspect ratio.
            var largest = Mathf.Max(resolution.x, resolution.y);
            if (largest > maximum)
            {
                var scale = maximum / largest;
                resolution *= scale;
            }

            return new(Mathf.CeilToInt(resolution.x), Mathf.CeilToInt(resolution.y));
        }

        internal static Vector2Int CalculateResolutionFromTexelSize(Vector2 worldSize, float texelSize, int maximum)
        {
            return CalculateResolution(new(worldSize.x / texelSize, worldSize.y / texelSize), maximum);
        }

        internal static Vector2Int CalculateResolutionFromTexelDensity(Vector2 worldSize, float texelDensity, int maximum)
        {
            return CalculateResolution(new(Mathf.RoundToInt(texelDensity * worldSize.x), Mathf.RoundToInt(texelDensity * worldSize.y)), maximum);
        }

        /// <summary>
        /// Rotates an XZ size and returns an XZ size which encapsulates it.
        /// </summary>
        public static Vector2 RotateAndEncapsulateXZ(Vector2 size, float angle)
        {
            angle = Mathf.PingPong(angle, 90f);
            var c = Mathf.Cos(angle * Mathf.Deg2Rad);
            var s = Mathf.Sin(angle * Mathf.Deg2Rad);
            return new
            (
                size.x * c + size.y * s,
                size.y * c + size.x * s
            );
        }

        public static BindingFlags s_AnyMethod = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
            BindingFlags.Static;

        public static T GetCustomAttribute<T>(System.Type type) where T : System.Attribute
        {
            return (T)System.Attribute.GetCustomAttribute(type, typeof(T));
        }

        public static WaitForEndOfFrame WaitForEndOfFrame { get; } = new();

        static Material s_UtilityMaterial;
        public static Material UtilityMaterial
        {
            get
            {
                if (s_UtilityMaterial == null)
                {
                    s_UtilityMaterial = new(Shader.Find("Hidden/Crest/Utility/Blit"));
                }

                return s_UtilityMaterial;
            }
        }

        // Need to cast to int but no conversion cost.
        // https://stackoverflow.com/a/69148528
        internal enum UtilityPass
        {
            CopyColor,
            CopyDepth,
            ClearDepth,
            ClearStencil,
            Copy,
        }

        // Taken from:
        // https://github.com/Unity-Technologies/Graphics/blob/871df5563d88e1ba778c82a43f39c9afc95368e6/Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl#L1149-L1152
        // Z buffer to linear 0..1 depth (0 at camera position, 1 at far plane).
        // Does NOT work with orthographic projections.
        // Does NOT correctly handle oblique view frustums.
        public static float NonLinearToLinear01Depth(float depth, Vector4 zBufferParameters)
        {
            return 1.0f / (zBufferParameters.x * depth + zBufferParameters.y);
        }

        // Taken from:
        // https://github.com/Unity-Technologies/Graphics/blob/871df5563d88e1ba778c82a43f39c9afc95368e6/Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl#L1154-L1161
        // Z buffer to linear depth.
        // Does NOT correctly handle oblique view frustums.
        // Does NOT work with orthographic projection.
        public static float NonLinearToLinearEyeDepth(float depth, Vector4 zBufferParameters)
        {
            return 1.0f / (zBufferParameters.z * depth + zBufferParameters.w);
        }

        // Taken from:
        // https://www.cyanilux.com/tutorials/depth/#depth-output
        public static float LinearDepthToNonLinear(float depth, Vector4 zBufferParameters)
        {
            return (1.0f - depth * zBufferParameters.y) / (depth * zBufferParameters.x);
        }

        // Taken from:
        // https://www.cyanilux.com/tutorials/depth/#depth-output
        public static float EyeDepthToNonLinear(float depth, Vector4 zBufferParameters)
        {
            return (1.0f - depth * zBufferParameters.w) / (depth * zBufferParameters.z);
        }

        public static Vector4 GetZBufferParameters(Camera camera)
        {
            // Taken and modified from:
            // https://github.com/Unity-Technologies/Graphics/blob/871df5563d88e1ba778c82a43f39c9afc95368e6/Packages/com.unity.render-pipelines.universal/Runtime/ScriptableRenderer.cs#L303-L327
            var near = camera.nearClipPlane;
            var far = camera.farClipPlane;
            var inverseNear = Mathf.Approximately(near, 0.0f) ? 0.0f : 1.0f / near;
            var inverseFar = Mathf.Approximately(far, 0.0f) ? 0.0f : 1.0f / far;

            // From http://www.humus.name/temp/Linearize%20depth.txt
            // But as depth component textures on OpenGL always return in 0..1 range (as in D3D), we have to use
            // the same constants for both D3D and OpenGL here.
            // OpenGL would be this:
            // zc0 = (1.0 - far / near) / 2.0;
            // zc1 = (1.0 + far / near) / 2.0;
            // D3D is this:
            var zc0 = 1.0f - far * inverseNear;
            var zc1 = far * inverseNear;

            var zBufferParameters = new Vector4(zc0, zc1, zc0 * inverseFar, zc1 * inverseFar);

            if (SystemInfo.usesReversedZBuffer)
            {
                zBufferParameters.y += zBufferParameters.x;
                zBufferParameters.x = -zBufferParameters.x;
                zBufferParameters.w += zBufferParameters.z;
                zBufferParameters.z = -zBufferParameters.z;
            }

            return zBufferParameters;
        }

        /// <summary>
        /// Uses PrefabUtility.InstantiatePrefab in editor and GameObject.Instantiate in standalone.
        /// </summary>
        public static GameObject InstantiatePrefab(GameObject prefab)
        {
#if UNITY_EDITOR
            return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#else
            return GameObject.Instantiate(prefab);
#endif
        }

        // Taken from Unity
        // https://docs.unity3d.com/2022.2/Documentation/Manual/BestPracticeUnderstandingPerformanceInUnity5.html
        public static bool StartsWithNoAlloc(this string a, string b)
        {
            var aLen = a.Length;
            var bLen = b.Length;

            var ap = 0; var bp = 0;

            while (ap < aLen && bp < bLen && a[ap] == b[bp])
            {
                ap++;
                bp++;
            }

            return bp == bLen;
        }

        public static void ReadRenderTexturePixels(ref RenderTexture rt, ref Texture2D texture, int slice = -1)
        {
            var source = rt;
            var copy = slice > -1 && rt.volumeDepth > 1;

            if (copy)
            {
                var descriptor = rt.descriptor;
                descriptor.volumeDepth = 1;
                source = RenderTexture.GetTemporary(descriptor);
                Graphics.CopyTexture(rt, slice, 0, source, 0, 0);
            }

            var previous = RenderTexture.active;
            RenderTexture.active = source;
            texture.ReadPixels(new(0, 0, texture.width, texture.height), 0, 0, false);
            texture.Apply();
            RenderTexture.active = previous;

            if (copy)
            {
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(source);
            }
        }

        // Reads a single pixel.
        public static void ReadRenderTexturePixel(ref RenderTexture rt, ref Texture2D texture, int x, int y, int slice = 0)
        {
            var descriptor = rt.descriptor;
            descriptor.width = 1;
            descriptor.height = 1;
            descriptor.volumeDepth = 1;
            var source = RenderTexture.GetTemporary(descriptor);
            Graphics.CopyTexture(rt, slice, 0, x, y, 1, 1, source, 0, 0, 0, 0);

            var previous = RenderTexture.active;
            RenderTexture.active = source;
            texture.ReadPixels(new(0, 0, texture.width, texture.height), 0, 0, false);
            texture.Apply();
            RenderTexture.active = previous;

            RenderTexture.ReleaseTemporary(source);
        }

        public static void Blit(RenderTexture source, RenderTexture target)
        {
            var active = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = active;
        }

        public static float ConvertDepthBufferValueToDistance(Camera camera, float depth)
        {
            float zBufferParamsX; float zBufferParamsY;
            if (SystemInfo.usesReversedZBuffer)
            {
                zBufferParamsY = 1f;
                zBufferParamsX = camera.farClipPlane / camera.nearClipPlane - 1f;
            }
            else
            {
                zBufferParamsY = camera.farClipPlane / camera.nearClipPlane;
                zBufferParamsX = 1f - zBufferParamsY;
            }

            return 1.0f / (zBufferParamsX / camera.farClipPlane * depth + zBufferParamsY / camera.farClipPlane);
        }

#if UNITY_EDITOR
        public static bool IsPreviewOfGameCamera(Camera camera)
        {
            // StartsWith has GC allocations. It is only used in the editor.
            return camera.cameraType == CameraType.Preview && camera.name == "Preview Camera";
        }
#endif

        public static bool IsMSAAEnabled(Camera camera)
        {
#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                var hdCamera = HDCamera.GetOrCreate(camera);
                // Scene view camera does appear to support MSAA unlike other RPs.
                // Querying frame settings on the camera will give the correct results - overriden or not.
                return hdCamera.msaaSamples != MSAASamples.None;
            }
#endif

            var isMSAA = camera.allowMSAA;
#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal)
            {
                // MSAA will be the same for every camera if XR rendering.
                isMSAA = isMSAA || XRHelpers.IsRunning;
            }
#endif

#if UNITY_EDITOR
            // Game View Preview ignores allowMSAA.
            isMSAA = isMSAA || IsPreviewOfGameCamera(camera);
            // Scene view doesn't support MSAA.
            isMSAA = isMSAA && camera.cameraType != CameraType.SceneView;
#endif

#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal)
            {
                // Keep this check last so it overrides everything else.
                isMSAA = isMSAA && camera.GetUniversalAdditionalCameraData().scriptableRenderer.supportedRenderingFeatures.msaa;
            }
#endif

            // QualitySettings.antiAliasing can be zero.
            return (isMSAA ? QualitySettings.antiAliasing : 1) > 1;
        }

        public static bool IsIntelGPU()
        {
            // Works for Windows and MacOS. Grabbed from Unity Graphics repository:
            // https://github.com/Unity-Technologies/Graphics/blob/68b0d42c/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/HDRenderPipeline.PostProcess.cs#L198-L199
            return SystemInfo.graphicsDeviceName.ToLowerInvariant().Contains("intel");
        }

        public static bool MaskIncludesLayer(int mask, int layer)
        {
            // Taken from:
            // http://answers.unity.com/answers/1332280/view.html
            return mask == (mask | (1 << layer));
        }

        // R16G16B16A16_SFloat appears to be the most compatible format.
        // https://docs.unity3d.com/Manual/class-TextureImporterOverride.html#texture-compression-support-platforms
        // https://learn.microsoft.com/en-us/windows/win32/direct3d12/typed-unordered-access-view-loads#supported-formats-and-api-calls
        static readonly GraphicsFormat s_FallbackGraphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

        static bool SupportsRandomWriteOnRenderTextureFormat(GraphicsFormat format)
        {
            var rtFormat = GraphicsFormatUtility.GetRenderTextureFormat(format);
            return System.Enum.IsDefined(typeof(RenderTextureFormat), rtFormat)
                && SystemInfo.SupportsRandomWriteOnRenderTextureFormat(rtFormat);
        }

        internal static GraphicsFormat GetCompatibleTextureFormat(GraphicsFormat format, GraphicsFormatUsage usage, string label, bool randomWrite = false)
        {
            var result = SystemInfo.GetCompatibleFormat(format, usage);
            var useFallback = result == GraphicsFormat.None;

#if CREST_DEBUG_LOG_FORMAT_CHANGES
            if (useFallback)
            {
                Debug.Log($"Crest: The graphics device does not support the render texture format {format}. Will attempt to use fallback. ({label})");
            }
            else if (result != format)
            {
                Debug.Log($"Crest: Using render texture format {result} instead of {format}. ({label})");
            }
#endif

            // NOTE: Disabling for now. RenderTextureFormat is a subset of GraphicsFormat and
            // there is not always an equivalent.
            // if (!useFallback && randomWrite && !SupportsRandomWriteOnRenderTextureFormat(result))
            // {
            //     Debug.Log($"Crest: The graphics device does not support the render texture format {result} with random read/write. Will attempt to use fallback.");
            //     useFallback = true;
            // }

            // Check if fallback is compatible before using it.
            if (useFallback && format == s_FallbackGraphicsFormat)
            {
                Debug.Log($"Crest: Fallback {s_FallbackGraphicsFormat} is not supported on this device. Please inform us. ({label})");
                useFallback = false;
            }

            if (useFallback)
            {
                result = s_FallbackGraphicsFormat;
            }

            return result;
        }

        public static void SetGlobalKeyword(string keyword, bool enabled)
        {
            if (enabled)
            {
                Shader.EnableKeyword(keyword);
            }
            else
            {
                Shader.DisableKeyword(keyword);
            }
        }

        public static void RenderTargetIdentifierXR(ref RenderTexture texture, ref RenderTargetIdentifier target)
        {
            target = new
            (
                texture,
                mipLevel: 0,
                CubemapFace.Unknown,
                depthSlice: -1 // Bind all XR slices.
            );
        }

        public static RenderTargetIdentifier RenderTargetIdentifierXR(int id) => new
        (
            id,
            mipLevel: 0,
            CubemapFace.Unknown,
            depthSlice: -1  // Bind all XR slices.
        );

        /// <summary>
        /// Creates an RT reference and adds it to the RTI. Native object behind RT is not created so you can change its
        /// properties before being used.
        /// </summary>
        public static void CreateRenderTargetTextureReference(ref RenderTexture texture, ref RenderTargetIdentifier target)
        {
            // Do not overwrite reference or it will create reference leak.
            if (texture == null)
            {
                // Dummy values. We are only creating an RT reference, not an RT native object. RT should be configured
                // properly before using or calling Create.
                texture = new(0, 0, 0);
            }

            // Always call this in case of recompilation as RTI will lose its reference to the RT.
            RenderTargetIdentifierXR(ref texture, ref target);
        }

        /// <summary>
        /// Creates an RT with an RTD if it does not exist or assigns RTD to RT (RT should be released first). This
        /// prevents reference leaks.
        /// </summary>
        /// <remarks>
        /// Afterwards call <a href="https://docs.unity3d.com/ScriptReference/RenderTexture.Create.html">Create</a> if
        /// necessary or <a href="https://docs.unity3d.com/ScriptReference/RenderTexture-active.html">let Unity handle
        /// it</a>.
        /// </remarks>
        public static void SafeCreateRenderTexture(ref RenderTexture texture, RenderTextureDescriptor descriptor)
        {
            // Do not overwrite reference or it will create reference leak.
            if (texture == null)
            {
                texture = new(descriptor);
            }
            else
            {
                if (texture.IsCreated())
                {
                    texture.Release();
                }

                texture.descriptor = descriptor;
            }
        }

        public static void SafeCreateRenderTexture(string name, ref RenderTexture texture, RenderTextureDescriptor descriptor)
        {
            // Do not overwrite reference or it will create reference leak.
            if (texture == null)
            {
                texture = new(descriptor);
                texture.name = name;
            }
            else
            {
                if (texture.IsCreated())
                {
                    texture.Release();
                }

                texture.descriptor = descriptor;
            }

            texture.Create();
        }

        public static void ClearRenderTexture(RenderTexture texture, Color clear, bool depth = true, bool color = true)
        {
            var active = RenderTexture.active;

            // Using RenderTexture.active will not write to all slices.
            Graphics.SetRenderTarget(texture, 0, CubemapFace.Unknown, -1);
            // TODO: Do we need to disable GL.sRGBWrite as it is linear to linear.
            GL.Clear(depth, color, clear);

            // Graphics.SetRenderTarget can be equivalent to setting RenderTexture.active:
            // https://docs.unity3d.com/ScriptReference/Graphics.SetRenderTarget.html
            // Restore previous active texture or it can incur a warning when releasing:
            // Releasing render texture that is set to be RenderTexture.active!
            RenderTexture.active = active;
        }

        public static void VerticallyFlipRenderTexture(RenderTexture target, bool force = false)
        {
            if (!force && !SystemInfo.graphicsUVStartsAtTop) return;
            var temporary = RenderTexture.GetTemporary(target.descriptor);
            Graphics.Blit(target, temporary, new Vector2(1, -1), new Vector2(0, 1));
            Graphics.Blit(temporary, target);
            RenderTexture.ReleaseTemporary(temporary);
        }

        public static bool RenderTargetTextureNeedsUpdating(RenderTexture texture, RenderTextureDescriptor descriptor)
        {
            return
                descriptor.width != texture.width ||
                descriptor.height != texture.height ||
                descriptor.volumeDepth != texture.volumeDepth ||
                descriptor.useDynamicScale != texture.useDynamicScale;
        }

        public static bool RenderTextureNeedsUpdating(RenderTexture t1, RenderTexture t2)
        {
            return
                t1.width != t2.width ||
                t1.height != t2.height ||
                t1.volumeDepth != t2.volumeDepth ||
                t1.graphicsFormat != t2.graphicsFormat;
        }

        public static bool RenderTextureNeedsUpdating(RenderTextureDescriptor t1, RenderTextureDescriptor t2)
        {
            return
                t1.width != t2.width ||
                t1.height != t2.height ||
                t1.volumeDepth != t2.volumeDepth ||
                t1.graphicsFormat != t2.graphicsFormat;
        }

        public static int CalculateMipMapCount(int maximumDimension)
        {
            return Mathf.FloorToInt(Mathf.Log(maximumDimension, 2f));
        }

        /// <summary>
        /// Uses Destroy in play mode or DestroyImmediate in edit mode.
        /// </summary>
        public static void Destroy(Object @object)
        {
#if UNITY_EDITOR
            // We must use DestroyImmediate in edit mode. As it apparently has an overhead, use recommended Destroy in
            // play mode. DestroyImmediate is generally recommended in edit mode by Unity:
            // https://docs.unity3d.com/ScriptReference/Object.DestroyImmediate.html
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(@object);
            }
            else
#endif
            {
                Object.Destroy(@object);
            }
        }

        // Borrowed from SRP code:
        // https://github.com/Unity-Technologies/Graphics/blob/7d292932bec3b4257a4defaf698fc7d77e2027f5/com.unity.render-pipelines.high-definition/Runtime/Core/Utilities/GeometryUtils.cs#L181-L184
        public static Matrix4x4 CalculateWorldToCameraMatrixRHS(Vector3 position, Quaternion rotation)
        {
            return Matrix4x4.Scale(new(1, 1, -1)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
        }

        /// <summary>
        /// Blit using full screen triangle. Supports more features than CommandBuffer.Blit like the RenderPipeline tag
        /// in sub-shaders. Never use for data.
        /// </summary>
        public static void Blit(CommandBuffer buffer, RenderTargetIdentifier target, Material material, int pass = -1, MaterialPropertyBlock properties = null)
        {
            if (!RenderPipelineHelper.IsLegacy)
            {
                CoreUtils.SetRenderTarget(buffer, target);
            }
            else
            {
                buffer.SetRenderTarget(target);
            }

            buffer.DrawProcedural
            (
                Matrix4x4.identity,
                material,
                pass,
                MeshTopology.Triangles,
                vertexCount: 3,
                instanceCount: 1,
                properties
            );
        }

        /// <summary>
        /// Blit using full screen triangle. Supports more features than CommandBuffer.Blit like the RenderPipeline tag
        /// in sub-shaders. Never use for fullscreen effects.
        /// </summary>
        public static void Blit(CommandBuffer buffer, RenderTexture target, Material material, int pass = -1, int depthSlice = -1, MaterialPropertyBlock properties = null)
        {
            buffer.SetRenderTarget(target, mipLevel: 0, CubemapFace.Unknown, depthSlice);
            buffer.DrawProcedural
            (
                Matrix4x4.identity,
                material,
                pass,
                MeshTopology.Triangles,
                vertexCount: 3,
                instanceCount: 1,
                properties
            );
        }

        // Fixes a bug with Unity, as we should not have to do this ourselves.
        // Only required under the following conditions:
        // Camera > URP Dynamic Resolution = checked
        // URP Asset > Anti Aliasing (MSAA) = unchecked
        // URP Asset > Upscale Filter = Spatial-Temporal Post-Processing
        [System.Diagnostics.Conditional("d_UnityURP")]
        public static void ScaleViewport(Camera camera, CommandBuffer buffer, RTHandle handle)
        {
            // Only applies to URP.
            if (!RenderPipelineHelper.IsUniversal) return;

            // Causes problems if we continue when this is checked.
            if (camera.allowDynamicResolution) return;

            var size = handle.GetScaledSize(handle.rtHandleProperties.currentViewportSize);
            if (size == Vector2Int.zero) return;
            buffer.SetViewport(new(0f, 0f, size.x, size.y));
        }

        public static void SetShaderVector(Material material, int nameID, Vector4 value, bool global = false)
        {
            if (global)
            {
                Shader.SetGlobalVector(nameID, value);
            }
            else
            {
                material.SetVector(nameID, value);
            }
        }

        public static void SetShaderInteger(Material material, int nameID, int value, bool global = false)
        {
            if (global)
            {
                Shader.SetGlobalInteger(nameID, value);
            }
            else
            {
                material.SetInteger(nameID, value);
            }
        }

        public static void SetShaderFloat(Material material, int nameID, float value, bool global = false)
        {
            if (global)
            {
                Shader.SetGlobalFloat(nameID, value);
            }
            else
            {
                material.SetFloat(nameID, value);
            }
        }

        public static bool GetGlobalBoolean(int id)
        {
            return Shader.GetGlobalInteger(id) == 1;
        }

        public static void SetGlobalBoolean(int id, bool value)
        {
            Shader.SetGlobalInteger(id, value ? 1 : 0);
        }

#if d_UnityURP
        static readonly List<bool> s_RenderFeatureActiveStates = new();
        static readonly FieldInfo s_RenderDataListField = typeof(UniversalRenderPipelineAsset)
                        .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_DefaultRendererIndex = typeof(UniversalRenderPipelineAsset)
                        .GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_RendererIndex = typeof(UniversalAdditionalCameraData)
                        .GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static ScriptableRendererData[] UniversalRendererData(UniversalRenderPipelineAsset asset) =>
                    (ScriptableRendererData[])s_RenderDataListField.GetValue(asset);

        internal static int GetRendererIndex(Camera camera)
        {
            var rendererIndex = (int)s_RendererIndex.GetValue(camera.GetUniversalAdditionalCameraData());

            if (rendererIndex < 0)
            {
                rendererIndex = (int)s_DefaultRendererIndex.GetValue(UniversalRenderPipeline.asset);
            }

            return rendererIndex;
        }

        internal static bool IsSSAOEnabled(Camera camera)
        {
            // Get this every time as it could change.
            var renderers = (ScriptableRendererData[])s_RenderDataListField.GetValue(UniversalRenderPipeline.asset);
            var rendererIndex = GetRendererIndex(camera);

            foreach (var feature in renderers[rendererIndex].rendererFeatures)
            {
                if (feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    return feature.isActive;
                }
            }

            return false;
        }

        internal static void RenderCameraWithoutCustomPasses(Camera camera)
        {
            // Get this every time as it could change.
            var renderers = (ScriptableRendererData[])s_RenderDataListField.GetValue(UniversalRenderPipeline.asset);
            var rendererIndex = GetRendererIndex(camera);

            foreach (var feature in renderers[rendererIndex].rendererFeatures)
            {
                // Null exception reported here. Might be null due to missing render features
                if (feature == null) continue;
                s_RenderFeatureActiveStates.Add(feature.isActive);
                feature.SetActive(false);
            }

            camera.Render();

            var index = 0;
            foreach (var feature in renderers[rendererIndex].rendererFeatures)
            {
                if (feature == null) continue;
                feature.SetActive(s_RenderFeatureActiveStates[index++]);
            }

            s_RenderFeatureActiveStates.Clear();
        }

        static readonly UniversalRenderPipeline.SingleCameraRequest s_RenderSingleCameraRequest = new();
#endif

        static readonly UnityEngine.Rendering.RenderPipeline.StandardRequest s_RenderStandardRequest = new();

        public static void RenderCamera(Camera camera, ScriptableRenderContext context)
        {
#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal)
            {
#if UNITY_6000_0_OR_NEWER
                // SingleCameraRequest does not render the full camera stack, thus should exclude
                // overlays which is likely desirable. Alternative approach worth investigating:
                // https://docs.unity3d.com/6000.0/Documentation/Manual/urp/User-Render-Requests.html
                // TODO: pass destination texture and slice instead of copying later.
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, s_RenderSingleCameraRequest);
#else
#pragma warning disable CS0618 // Type or member is obsolete
                UniversalRenderPipeline.RenderSingleCamera(context, camera);
#pragma warning restore CS0618 // Type or member is obsolete
#endif
                return;
            }
#endif

#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                camera.SubmitRenderRequest(s_RenderStandardRequest);
                return;
            }
#endif

            camera.Render();
        }

    }

    // Terrain
    static partial class Helpers
    {
        static readonly List<Terrain> s_Terrains = new();
        internal static Terrain GetTerrainAtPosition(Vector2 position)
        {
            Terrain.GetActiveTerrains(s_Terrains);

            foreach (var terrain in s_Terrains)
            {
                var rect = new Rect(terrain.transform.position.XZ(), terrain.terrainData.size.XZ());

                // Return the first one.
                if (rect.Contains(position))
                {
                    return terrain;
                }
            }

            return null;
        }
    }

    namespace Internal
    {
        static class Extensions
        {
            // Swizzle
            public static Vector2 XZ(this Vector3 v) => new(v.x, v.z);
            public static Vector2 XY(this Vector4 v) => new(v.x, v.y);
            public static Vector2 ZW(this Vector4 v) => new(v.z, v.w);
            public static Vector3 XYZ(this Vector4 v) => new(v.x, v.y, v.z);
            public static Vector3 XNZ(this Vector2 v, float n = 0f) => new(v.x, n, v.y);
            public static Vector3 XNZ(this Vector3 v, float n = 0f) => new(v.x, n, v.z);
            public static Vector3 XNN(this Vector3 v, float n = 0f) => new(v.x, n, n);
            public static Vector3 NNZ(this Vector3 v, float n = 0f) => new(n, n, v.z);
            public static Vector3 NYN(this Vector3 v, float n = 0f) => new(n, v.y, n);
            public static Vector4 XYZN(this Vector3 v, float n = 0f) => new(v.x, v.y, v.z, n);
            public static Vector4 XYNN(this Vector2 v, float n = 0f) => new(v.x, v.y, n, n);
            public static Vector4 XYNN(this Vector2 v, Vector2 n) => new(v.x, v.y, n.x, n.y);
            public static Vector4 NNZW(this Vector2 v, float n = 0f) => new(n, n, v.x, v.y);
            public static float Maximum(this Vector3 v) => Mathf.Max(Mathf.Max(v.x, v.y), v.z);

            public static Vector2 Absolute(this Vector2 v) => new
            (
                Mathf.Abs(v.x),
                Mathf.Abs(v.y)
            );

            public static Color Clamped01(this Color c) => new
            (
                Mathf.Clamp01(c.r),
                Mathf.Clamp01(c.g),
                Mathf.Clamp01(c.b),
                Mathf.Clamp01(c.a)
            );

            public static void SetKeyword(this Material material, string keyword, bool enabled)
            {
                if (enabled)
                {
                    material.EnableKeyword(keyword);
                }
                else
                {
                    material.DisableKeyword(keyword);
                }
            }

            public static void SetKeyword(this ComputeShader shader, string keyword, bool enabled)
            {
                if (enabled)
                {
                    shader.EnableKeyword(keyword);
                }
                else
                {
                    shader.DisableKeyword(keyword);
                }
            }

            public static void SetShaderKeyword(this CommandBuffer buffer, string keyword, bool enabled)
            {
                if (enabled)
                {
                    buffer.EnableShaderKeyword(keyword);
                }
                else
                {
                    buffer.DisableShaderKeyword(keyword);
                }
            }

            static readonly Vector3[] s_BoundsPoints = new Vector3[8];

            public static Bounds Bounds(this Transform transform)
            {
                var bounds = new Bounds();
                bounds.center = transform.position;
                var f = new Vector3(0.0f, 0.0f, 0.5f);
                var u = new Vector3(0.0f, 0.5f, 0.0f);
                var r = new Vector3(0.5f, 0.0f, 0.0f);
                bounds.Encapsulate(transform.TransformPoint(f + u + r));
                bounds.Encapsulate(transform.TransformPoint(-f + u + r));
                bounds.Encapsulate(transform.TransformPoint(f + -u + r));
                bounds.Encapsulate(transform.TransformPoint(f + u + -r));
                bounds.Encapsulate(transform.TransformPoint(-f + -u + r));
                bounds.Encapsulate(transform.TransformPoint(f + -u + -r));
                bounds.Encapsulate(transform.TransformPoint(-f + u + -r));
                bounds.Encapsulate(transform.TransformPoint(-f + -u + -r));
                return bounds;
            }

            /// <summary>
            /// Applys the transform to local bounds similar to Renderer.
            /// </summary>
            /// <param name="transform">The transform to apply to bounds.</param>
            /// <param name="bounds">Local bounds to transform.</param>
            /// <returns>Bounds with transform applied.</returns>
            public static Bounds TransformBounds(this Transform transform, Bounds bounds)
            {
                s_BoundsPoints[0] = bounds.min;
                s_BoundsPoints[1] = bounds.max;
                s_BoundsPoints[2] = new(bounds.min.x, bounds.min.y, bounds.max.z);
                s_BoundsPoints[3] = new(bounds.min.x, bounds.max.y, bounds.min.z);
                s_BoundsPoints[4] = new(bounds.max.x, bounds.min.y, bounds.min.z);
                s_BoundsPoints[5] = new(bounds.min.x, bounds.max.y, bounds.max.z);
                s_BoundsPoints[6] = new(bounds.max.x, bounds.min.y, bounds.max.z);
                s_BoundsPoints[7] = new(bounds.max.x, bounds.max.y, bounds.min.z);

                return GeometryUtility.CalculateBounds(s_BoundsPoints, transform.localToWorldMatrix);
            }

            public static Rect RectXZ(this Bounds bounds)
            {
                return Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
            }

            public static Rect RectXZ(this Transform transform)
            {
                var scale = transform.lossyScale.XZ();
                scale = Helpers.RotateAndEncapsulateXZ(scale, transform.rotation.eulerAngles.y);
                return new(transform.position.XZ() - scale * 0.5f, scale);
            }

            public static Vector2 RotationXZ(this Transform transform)
            {
                return new Vector2(transform.localToWorldMatrix.m20, transform.localToWorldMatrix.m00).normalized;
            }

            public static Color MaybeLinear(this Color color)
            {
                return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            }

            public static Color MaybeGamma(this Color color)
            {
                return QualitySettings.activeColorSpace == ColorSpace.Linear ? color : color.gamma;
            }

            public static Color FinalColor(this Light light)
            {
                var linear = GraphicsSettings.lightsUseLinearIntensity;
                var color = linear ? light.color.linear : light.color;
                color *= light.intensity;
                if (linear && light.useColorTemperature) color *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
                if (!linear) color = color.MaybeLinear();
                return linear ? color.MaybeGamma() : color;
            }

            ///<summary>
            /// Sets the msaaSamples property to the highest supported MSAA level in the settings.
            ///</summary>
            public static void SetMSAASamples(this ref RenderTextureDescriptor descriptor, Camera camera)
            {
                // QualitySettings.antiAliasing is zero when disabled which is invalid for msaaSamples.
                // We need to set this first as GetRenderTextureSupportedMSAASampleCount uses it:
                // https://docs.unity3d.com/ScriptReference/SystemInfo.GetRenderTextureSupportedMSAASampleCount.html
                descriptor.msaaSamples = Helpers.IsMSAAEnabled(camera) ? Mathf.Max(QualitySettings.antiAliasing, 1) : 1;
                descriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            }

            // RT descriptor from texture base class.
            internal static RenderTextureDescriptor GetDescriptor(this Texture texture)
            {
                if (texture is RenderTexture rt)
                {
                    return rt.descriptor;
                }

                var descriptor = new RenderTextureDescriptor(0, 0)
                {
                    width = texture.width,
                    height = texture.height,
                    graphicsFormat = texture.graphicsFormat,
                    dimension = texture.dimension,
                    volumeDepth = 1,
                    msaaSamples = 1,
                    useMipMap = false,
                    enableRandomWrite = true,
                };

                return descriptor;
            }

            public static bool GetBoolean(this Material material, int id)
            {
                return (material.HasInteger(id) ? material.GetInteger(id) : material.GetInt(id)) != 0;
            }

            public static void SetBoolean(this Material material, int id, bool value)
            {
                if (material.HasInteger(id))
                {
                    material.SetInteger(id, value ? 1 : 0);
                }
                else
                {
                    material.SetInt(id, value ? 1 : 0);
                }
            }

            public static void SetGlobalBoolean(this CommandBuffer buffer, int id, bool value)
            {
                buffer.SetGlobalInteger(id, value ? 1 : 0);
            }

            public static bool IsEmpty(this UnityEngine.Events.UnityEvent @event)
            {
                return @event.GetPersistentEventCount() == 0;
            }

            public static bool IsEmpty<T>(this UnityEngine.Events.UnityEvent<T> @event)
            {
                return @event.GetPersistentEventCount() == 0;
            }
        }
    }

    namespace Utility
    {
        /// <summary>
        /// Puts together a hash from given data values. More deterministic than
        /// System.HashCode across recompiles.
        /// </summary>
        static class Hash
        {
            public static int CreateHash() => 0x19384567;

            public static void AddFloat(float value, ref int hash)
            {
                // Appears to be deterministic across recompiles.
                hash ^= value.GetHashCode();
            }

            public static void AddInt(int value, ref int hash)
            {
                hash ^= value;
            }

            public static void AddBool(bool value, ref int hash)
            {
                hash ^= value ? 0x74659374 : 0x62649035;
            }

            public static void AddObject(object value, ref int hash)
            {
                // Will be the index of this object instance
                hash ^= value.GetHashCode();
            }

            public static void AddObject<T>(T value, ref int hash) where T : struct
            {
                // Will be the index of this object instance
                hash ^= value.GetHashCode();
            }
        }
    }
}
