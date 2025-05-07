// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Unified interface for setting properties on both materials and material property blocks
    /// </summary>
    interface IPropertyWrapper
    {
        void SetFloat(int param, float value);
        void SetFloatArray(int param, float[] value);
        void SetVector(int param, Vector4 value);
        void SetVectorArray(int param, Vector4[] value);
        void SetTexture(int param, Texture value);
        void SetMatrix(int param, Matrix4x4 matrix);
        void SetInteger(int param, int value);
        void SetBoolean(int param, bool value);
        void GetBlock();
        void SetBlock();
    }

    static class PropertyWrapperConstants
    {
        internal const string k_NoShaderMessage = "Cannot create required material because shader <i>{0}</i> could not be found or loaded."
            + " Try right clicking the Crest folder in the Project view and selecting Reimport, and checking for errors.";
    }

    readonly struct PropertyWrapperBuffer : IPropertyWrapper
    {
        public CommandBuffer Buffer { get; }
        public PropertyWrapperBuffer(CommandBuffer mpb) => Buffer = mpb;
        public void SetFloat(int param, float value) => Buffer.SetGlobalFloat(param, value);
        public void SetFloatArray(int param, float[] value) => Buffer.SetGlobalFloatArray(param, value);
        public void SetTexture(int param, Texture value) => Buffer.SetGlobalTexture(param, value);
        public void SetVector(int param, Vector4 value) => Buffer.SetGlobalVector(param, value);
        public void SetVectorArray(int param, Vector4[] value) => Buffer.SetGlobalVectorArray(param, value);
        public void SetMatrix(int param, Matrix4x4 value) => Buffer.SetGlobalMatrix(param, value);
        public void SetInteger(int param, int value) => Buffer.SetGlobalInteger(param, value);
        public void SetBoolean(int param, bool value) => Buffer.SetGlobalInteger(param, value ? 1 : 0);

        public void GetBlock() { }
        public void SetBlock() { }
    }

    readonly struct PropertyWrapperRenderer : IPropertyWrapper
    {
        public MaterialPropertyBlock PropertyBlock { get; }
        public Renderer Renderer { get; }

        public PropertyWrapperRenderer(Renderer renderer, MaterialPropertyBlock block)
        {
            Renderer = renderer;
            PropertyBlock = block;
        }

        public void SetFloat(int param, float value) => PropertyBlock.SetFloat(param, value);
        public void SetFloatArray(int param, float[] value) => PropertyBlock.SetFloatArray(param, value);
        public void SetTexture(int param, Texture value) => PropertyBlock.SetTexture(param, value);
        public void SetBuffer(int param, ComputeBuffer value) => PropertyBlock.SetBuffer(param, value);
        public void SetVector(int param, Vector4 value) => PropertyBlock.SetVector(param, value);
        public void SetVectorArray(int param, Vector4[] value) => PropertyBlock.SetVectorArray(param, value);
        public void SetMatrix(int param, Matrix4x4 value) => PropertyBlock.SetMatrix(param, value);
        public void SetInteger(int param, int value) => PropertyBlock.SetInteger(param, value);
        public void SetBoolean(int param, bool value) => PropertyBlock.SetInteger(param, value ? 1 : 0);

        public void GetBlock() => Renderer.GetPropertyBlock(PropertyBlock);
        public void SetBlock() => Renderer.SetPropertyBlock(PropertyBlock);
    }

    [System.Serializable]
    readonly struct PropertyWrapperMaterial : IPropertyWrapper
    {
        public Material Material { get; }

        public PropertyWrapperMaterial(Material material) => Material = material;
        public PropertyWrapperMaterial(Shader shader)
        {
            Debug.Assert(shader != null, "Crest: PropertyWrapperMaterial: Cannot create required material because shader is null");
            Material = new(shader);
        }
        public PropertyWrapperMaterial(string shaderPath)
        {
            var shader = Shader.Find(shaderPath);
            Debug.AssertFormat(shader != null, $"Crest.PropertyWrapperMaterial: {PropertyWrapperConstants.k_NoShaderMessage}", shaderPath);
            Material = new(shader);
        }

        public void SetFloat(int param, float value) => Material.SetFloat(param, value);
        public void SetFloatArray(int param, float[] value) => Material.SetFloatArray(param, value);
        public void SetTexture(int param, Texture value) => Material.SetTexture(param, value);
        public void SetBuffer(int param, ComputeBuffer value) => Material.SetBuffer(param, value);
        public void SetVector(int param, Vector4 value) => Material.SetVector(param, value);
        public void SetVectorArray(int param, Vector4[] value) => Material.SetVectorArray(param, value);
        public void SetMatrix(int param, Matrix4x4 value) => Material.SetMatrix(param, value);
        public void SetInteger(int param, int value) => Material.SetInteger(param, value);
        public void SetBoolean(int param, bool value) => Material.SetInteger(param, value ? 1 : 0);

        public void GetBlock() { }
        public void SetBlock() { }

        // Non-Interface Methods
        public void SetKeyword(in LocalKeyword keyword, bool value) => Material.SetKeyword(keyword, value);
    }

    readonly struct PropertyWrapperMPB : IPropertyWrapper
    {
        public MaterialPropertyBlock MaterialPropertyBlock { get; }
        public PropertyWrapperMPB(MaterialPropertyBlock mpb) => MaterialPropertyBlock = mpb;
        public void SetFloat(int param, float value) => MaterialPropertyBlock.SetFloat(param, value);
        public void SetFloatArray(int param, float[] value) => MaterialPropertyBlock.SetFloatArray(param, value);
        public void SetTexture(int param, Texture value) => MaterialPropertyBlock.SetTexture(param, value);
        public void SetVector(int param, Vector4 value) => MaterialPropertyBlock.SetVector(param, value);
        public void SetVectorArray(int param, Vector4[] value) => MaterialPropertyBlock.SetVectorArray(param, value);
        public void SetMatrix(int param, Matrix4x4 value) => MaterialPropertyBlock.SetMatrix(param, value);
        public void SetInteger(int param, int value) => MaterialPropertyBlock.SetInteger(param, value);
        public void SetBoolean(int param, bool value) => MaterialPropertyBlock.SetInteger(param, value ? 1 : 0);

        public void GetBlock() { }
        public void SetBlock() { }
    }

    [System.Serializable]
    readonly struct PropertyWrapperCompute : IPropertyWrapper
    {
        readonly CommandBuffer _Buffer;
        readonly ComputeShader _Shader;
        readonly int _Kernel;

        public PropertyWrapperCompute(CommandBuffer buffer, ComputeShader shader, int kernel)
        {
            _Buffer = buffer;
            _Shader = shader;
            _Kernel = kernel;
        }

        public void SetFloat(int param, float value) => _Buffer.SetComputeFloatParam(_Shader, param, value);
        public void SetFloatArray(int param, float[] value) => _Buffer.SetGlobalFloatArray(param, value);
        public void SetInteger(int param, int value) => _Buffer.SetComputeIntParam(_Shader, param, value);
        public void SetBoolean(int param, bool value) => _Buffer.SetComputeIntParam(_Shader, param, value ? 1 : 0);
        public void SetTexture(int param, Texture value) => _Buffer.SetComputeTextureParam(_Shader, _Kernel, param, value);
        public void SetTexture(int param, RenderTargetIdentifier value) => _Buffer.SetComputeTextureParam(_Shader, _Kernel, param, value);
        public void SetBuffer(int param, ComputeBuffer value) => _Buffer.SetComputeBufferParam(_Shader, _Kernel, param, value);
        public void SetVector(int param, Vector4 value) => _Buffer.SetComputeVectorParam(_Shader, param, value);
        public void SetVectorArray(int param, Vector4[] value) => _Buffer.SetComputeVectorArrayParam(_Shader, param, value);
        public void SetMatrix(int param, Matrix4x4 value) => _Buffer.SetComputeMatrixParam(_Shader, param, value);

        public void GetBlock() { }
        public void SetBlock() { }

        // Non-Interface Methods
        public void SetKeyword(in LocalKeyword keyword, bool value) => _Buffer.SetKeyword(_Shader, keyword, value);
        public void Dispatch(int x, int y, int z) => _Buffer.DispatchCompute(_Shader, _Kernel, x, y, z);
    }

    [System.Serializable]
    readonly struct PropertyWrapperComputeStandalone : IPropertyWrapper
    {
        readonly ComputeShader _Shader;
        readonly int _Kernel;

        public PropertyWrapperComputeStandalone(ComputeShader shader, int kernel)
        {
            _Shader = shader;
            _Kernel = kernel;
        }

        public void SetFloat(int param, float value) => _Shader.SetFloat(param, value);
        public void SetFloatArray(int param, float[] value) => _Shader.SetFloats(param, value);
        public void SetInteger(int param, int value) => _Shader.SetInt(param, value);
        public void SetBoolean(int param, bool value) => _Shader.SetBool(param, value);
        public void SetTexture(int param, Texture value) => _Shader.SetTexture(_Kernel, param, value);
        public void SetBuffer(int param, ComputeBuffer value) => _Shader.SetBuffer(_Kernel, param, value);
        public void SetConstantBuffer(int param, ComputeBuffer value) => _Shader.SetConstantBuffer(param, value, 0, value.stride);
        public void SetVector(int param, Vector4 value) => _Shader.SetVector(param, value);
        public void SetVectorArray(int param, Vector4[] value) => _Shader.SetVectorArray(param, value);
        public void SetMatrix(int param, Matrix4x4 value) => _Shader.SetMatrix(param, value);

        public void GetBlock() { }
        public void SetBlock() { }

        // Non-Interface Methods
        public void SetKeyword(in LocalKeyword keyword, bool value) => _Shader.SetKeyword(keyword, value);
        public void Dispatch(int x, int y, int z) => _Shader.Dispatch(_Kernel, x, y, z);
    }
}
