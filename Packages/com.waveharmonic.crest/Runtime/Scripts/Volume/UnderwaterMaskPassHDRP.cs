// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityHDRP

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace WaveHarmonic.Crest
{
    sealed class UnderwaterMaskPassHDRP : CustomPass
    {
        const string k_Name = "Underwater Mask";

        static UnderwaterRenderer s_Renderer;
        static UnderwaterMaskPass s_UnderwaterMaskPass;
        static UnderwaterMaskPassHDRP s_Instance;
        GameObject _GameObject;

        public static void Enable(UnderwaterRenderer renderer)
        {
            var gameObject = CustomPassHelpers.CreateOrUpdate
            (
                parent: renderer._Water.Container.transform,
                k_Name,
                hide: !renderer._Water._Debug._ShowHiddenObjects
            );

            CustomPassHelpers.CreateOrUpdate
            (
                gameObject,
                ref s_Instance,
                k_Name,
                CustomPassInjectionPoint.BeforeRendering
            );

            s_Instance._GameObject = gameObject;

            s_Renderer = renderer;
            s_UnderwaterMaskPass = new(renderer);
        }

        public static void Disable()
        {
            // It should be safe to rely on this reference for this reference to fail.
            if (s_Instance != null && s_Instance._GameObject != null)
            {
                // Will also trigger Cleanup below.
                s_Instance._GameObject.SetActive(false);
            }
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            s_UnderwaterMaskPass.Allocate();
        }

        protected override void Cleanup()
        {
            s_UnderwaterMaskPass?.Release();
        }

        protected override void Execute(CustomPassContext context)
        {
            var camera = context.hdCamera.camera;

            if (!s_Renderer.ShouldRender(camera, UnderwaterRenderer.Pass.Mask))
            {
                return;
            }

            s_UnderwaterMaskPass.Execute(camera, context.cmd);
        }
    }
}

#endif // d_UnityHDRP
