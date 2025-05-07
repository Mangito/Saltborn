// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityURP
#if UNITY_6000_0_OR_NEWER

using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    static class RenderGraphHelper
    {
        public struct Handle
        {
            RTHandle _RTHandle;
            TextureHandle _TextureHandle;

            public readonly RTHandle Texture { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _RTHandle ?? _TextureHandle; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Handle(RTHandle handle) => new() { _RTHandle = handle };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Handle(TextureHandle handle) => new() { _TextureHandle = handle };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator RTHandle(Handle texture) => texture.Texture;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator TextureHandle(Handle texture) => texture._TextureHandle;
        }

        static readonly FieldInfo s_RenderContext = typeof(InternalRenderGraphContext).GetField("renderContext", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_WrappedContext = typeof(UnsafeGraphContext).GetField("wrappedContext", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_FrameData = typeof(RenderingData).GetField("frameData", BindingFlags.NonPublic | BindingFlags.Instance);

        public static ScriptableRenderContext GetRenderContext(this UnsafeGraphContext unsafeContext)
        {
            return (ScriptableRenderContext)s_RenderContext.GetValue((InternalRenderGraphContext)s_WrappedContext.GetValue(unsafeContext));
        }

        public static ContextContainer GetFrameData(this ref RenderingData renderingData)
        {
            return (ContextContainer)s_FrameData.GetValue(renderingData);
        }
    }
}

#endif // UNITY_6000_0_OR_NEWER
#endif // d_UnityURP
