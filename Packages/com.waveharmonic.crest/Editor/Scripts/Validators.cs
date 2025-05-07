// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Watercraft;

using static WaveHarmonic.Crest.Editor.ValidatedHelper;
using MessageType = WaveHarmonic.Crest.Editor.ValidatedHelper.MessageType;

namespace WaveHarmonic.Crest.Editor
{
    static class Validators
    {
        internal static WaterRenderer Water => Utility.Water;

        [Validator(typeof(LodInput))]
        static bool ValidateTextureInput(LodInput target, ShowMessage messenger)
        {
            if (target.Data is not TextureLodInputData data) return true;

            var isValid = true;

            if (data._Texture == null)
            {
                messenger
                (
                    "Texture mode requires a texture.",
                    "Assign a texture.",
                    MessageType.Error,
                    target
                );
            }

            return isValid;
        }

        [Validator(typeof(LodInput))]
        static bool ValidateGeometryInput(LodInput target, ShowMessage messenger)
        {
            if (target.Data is not GeometryLodInputData data) return true;

            var isValid = true;

            if (data._Geometry == null)
            {
                messenger
                (
                    "Geometry mode requires geometry (ie mesh).",
                    "Assign geometry.",
                    MessageType.Error,
                    target
                );
            }

            return isValid;
        }

        [Validator(typeof(LodInput))]
        static bool ValidateRendererInput(LodInput target, ShowMessage messenger)
        {
            if (target.Data is not RendererLodInputData data) return true;

            // Check if Renderer component is attached.
            var isValid = ValidateRenderer<Renderer>
            (
                target,
                data._Renderer,
                messenger,
                data._CheckShaderPasses && (!data._OverrideShaderPass || data._ShaderPassIndex != -1),
                data._CheckShaderName ? data.ShaderPrefix : string.Empty
            );

            if (data._Renderer == null)
            {
                return isValid;
            }

            // Can cause problems if culling masks are used.
            if (!data._DisableRenderer)
            {
                isValid = ValidateRendererLayer(target.gameObject, messenger, Water) && isValid;
            }

            var isPersistent = target is FoamLodInput or DynamicWavesLodInput or ShadowLodInput;

            var materials = data._Renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;

                if (data._OverrideShaderPass && data._ShaderPassIndex > material.shader.passCount - 1)
                {
                    messenger
                    (
                        $"The shader <i>{material.shader.name}</i> used by this input has opted for the shader pass " +
                        $"index {data._ShaderPassIndex}, but there is only {material.shader.passCount} passes on the shader.",
                        "Choose a valid shader pass.",
                        MessageType.Error, target
                    );
                }

                if (isPersistent)
                {
                    if (material.shader.name is "Crest/Inputs/All/Utility" or "Crest/Inputs/All/Scale")
                    {
                        messenger
                        (
                            $"The shader <i>{material.shader.name}</i> currently is not supported by this simulation " +
                            "(Foam, Dynamic Waves or Shadow) as the shader does not support time steps.",
                            "Choose a valid shader (not <i>Crest/Inputs/All/Utility</i> or <i>Crest/Inputs/All/Scale</i>).",
                            MessageType.Error, target
                        );
                    }
                }
            }

            return isValid;
        }

        static bool ValidateRendererLayer(GameObject gameObject, ShowMessage messenger, WaterRenderer water)
        {
            if (water != null && gameObject.layer != water.Layer)
            {
                var layerName = LayerMask.LayerToName(water.Layer);
                messenger
                (
                    $"The layer is not the same as the <i>{nameof(WaterRenderer)}.{nameof(WaterRenderer.Layer)} ({layerName})</i> which can cause problems if the <i>{layerName}</i> layer is excluded from any culling masks.",
                    $"Set layer to <i>{layerName}</i>.",
                    MessageType.Warning, gameObject,
                    (_, _) =>
                    {
                        Undo.RecordObject(gameObject, $"Change Layer to {layerName}");
                        gameObject.layer = water.Layer;
                    }
                );
            }

            // Is valid as not outright invalid but could be.
            return true;
        }

        static bool Validate(WaterReflections target, ShowMessage messenger, WaterRenderer water)
        {
            var isValid = true;

            if (!target._Enabled)
            {
                return isValid;
            }

            var material = water.Material;

            if (material != null)
            {
                if (material.HasProperty(WaterRenderer.ShaderIDs.s_PlanarReflectionsEnabled) && material.GetFloat(WaterRenderer.ShaderIDs.s_PlanarReflectionsEnabled) == 0)
                {
                    messenger
                    (
                        $"<i>Planar Reflections</i> are not enabled on the <i>{material.name}</i> material and will not be visible.",
                        $"Enable <i>Planar Reflections</i> on the material (<i>{material.name}</i>) currently assigned to the <i>{nameof(WaterRenderer)}</i> component.",
                        MessageType.Warning, material
                    );
                }

                if (material.HasProperty(WaterRenderer.ShaderIDs.s_Occlusion) && target._Mode != WaterReflectionSide.Below && material.GetFloat(WaterRenderer.ShaderIDs.s_Occlusion) == 0)
                {
                    messenger
                    (
                        $"<i>Occlusion</i> is set to zero on the <i>{material.name}</i> material. Planar reflections will not be visible.",
                        $"Increase <i>Occlusion</i> on the material (<i>{material.name}</i>) currently assigned to the <i>{nameof(WaterRenderer)}</i> component.",
                        MessageType.Warning, material
                    );
                }

                if (material.HasProperty(WaterRenderer.ShaderIDs.s_OcclusionUnderwater) && target._Mode != WaterReflectionSide.Above && material.GetFloat(WaterRenderer.ShaderIDs.s_OcclusionUnderwater) == 0)
                {
                    messenger
                    (
                        $"<i>Occlusion (U)</i> is set to zero on the <i>{material.name}</i> material. Planar reflections will not be visible.",
                        $"Increase <i>Occlusion (U)</i> on the material (<i>{material.name}</i>) currently assigned to the <i>{nameof(WaterRenderer)}</i> component.",
                        MessageType.Warning, material
                    );
                }
            }

            if (!target._Sky)
            {
                messenger
                (
                    $"<i>Sky</i> on <i>Reflections</i> is not enabled. " +
                    "Any custom shaders which do not write alpha (eg some tree leaves) will not appear in the final reflections.",
                    "Enable <i>Sky</i>.",
                    MessageType.Info, target._Water,
                    (_, y) => y.boolValue = true,
                    $"{nameof(WaterRenderer._Reflections)}.{nameof(WaterReflections._Sky)}"

                );
            }

#if !UNITY_6000_0_OR_NEWER
#if d_UnityHDRP
            if (!target._RenderOnlySingleCamera && RenderPipelineHelper.IsHighDefinition)
            {
                messenger
                (
                    $"Please note that <i>Reflections > Render Only Single Camera</i> has no effect for Unity 2022.3 HDRP. " +
                    "It is forced to enabled, as HDRP cannot render to multiple cameras, as it requires recursive rendering.",
                    "Upgrade to Unity 6 if you need this feature.",
                    MessageType.Info, target._Water
                );
            }
#endif
#endif

            return isValid;
        }

        static bool Validate(UnderwaterRenderer target, ShowMessage messenger, WaterRenderer water)
        {
            var isValid = true;

            if (!target._Enabled)
            {
                return isValid;
            }

            if (water != null && water.Material != null)
            {
                var material = water.Material;

                var cullModeName =
#if d_UnityURP
                    RenderPipelineHelper.IsUniversal ? "_Cull" :
#endif
#if d_UnityHDRP
                    RenderPipelineHelper.IsHighDefinition ? "_CullMode" :
#endif
                    "_BUILTIN_CullMode";

                if (material.HasFloat(cullModeName) && material.GetFloat(cullModeName) == (int)CullMode.Back)
                {
                    messenger
                    (
                        $"<i>Cull Mode</i> is set to <i>Back</i> on material <i>{material.name}</i>. " +
                        "The underside of the water surface will not be rendered.",
                        $"Set <i>Cull Mode</i> to <i>Off</i> (or <i>Front</i>) on <i>{material.name}</i>.",
                        MessageType.Warning, material,
                        (material, _) =>
                        {
                            FixSetMaterialIntProperty(material, "Cull Mode", cullModeName, (int)CullMode.Off);
                            if (RenderPipelineHelper.IsHighDefinition)
                            {
                                // HDRP material will not update without viewing it...
                                Selection.activeObject = material.targetObject;
                            }
                        }
                    );
                }

#if d_UnityHDRP
                if (RenderPipelineHelper.IsHighDefinition)
                {
                    if (material.GetFloat(cullModeName) == (int)CullMode.Off && !material.IsKeywordEnabled("_DOUBLESIDED_ON"))
                    {
                        messenger
                        (
                            $"<i>Double-Sided</i> is not enabled on material <i>{material.name}</i>. " +
                            "The underside of the water surface will not be rendered correctly.",
                            $"Enable <i>Double-Sided</i> on <i>{material.name}</i>.",
                            MessageType.Warning, material,
                            (material, _) =>
                            {
                                FixSetMaterialOptionEnabled(material, "_DOUBLESIDED_ON", "_DoubleSidedEnable", enabled: true);
                                // HDRP material will not update without viewing it...
                                Selection.activeObject = material.targetObject;
                            }
                        );
                    }
                }
#endif
            }

            return isValid;
        }

        [Validator(typeof(WaterRenderer))]
        static bool Validate(WaterRenderer target, ShowMessage messenger)
        {
            var isValid = true;

            var water = target;

            isValid = isValid && Validate(target._Underwater, messenger, target);
            isValid = isValid && Validate(target._Reflections, messenger, target);
            isValid = isValid && ValidateNoRotation(target, target.transform, messenger);
            isValid = isValid && ValidateNoScale(target, target.transform, messenger);

#if CREST_OCEAN
            messenger
            (
                "The <i>CREST_OCEAN</i> scripting symbol is present from <i>Crest 4</i>. " +
                "This enables migration mode. Please read the documentation for the migration guide.",
                "Remove <i>CREST_OCEAN</i> from <i>Project Settings > Player > Other Settings > Scripting Define Symbols</i> once finished migrating.",
                MessageType.Info, target
            );
#endif

            if (target._Resources == null)
            {
                messenger
                (
                    "The Water Renderer is missing required internal data.",
                    "Populate required internal data.",
                    MessageType.Error, target,
                    (_, y) => y.objectReferenceValue = WaterResources.Instance,
                    nameof(target._Resources)
                );

                isValid = false;
            }

            if (target.Material == null)
            {
                messenger
                (
                    "No water material specified.",
                    $"Assign a valid water material to the Material property of the <i>{nameof(WaterRenderer)}</i> component.",
                    MessageType.Error, target
                );

                isValid = false;
            }
            else
            {
                isValid = ValidateWaterMaterial(target, messenger, water, target.Material) && isValid;

                if (RenderPipelineHelper.IsHighDefinition && target.Material.GetFloat("_RefractionModel") > 0)
                {
                    messenger
                    (
                        $"<i>Refraction Model</i> is not <i>None</i> for <i>{target.Material}</i>. " +
                        "This is set by default so it is available in the inspector, " +
                        "but it incurs an overhead and will produce a dark edge at the edge of the viewport (see <i>Screen Space Refraction > Screen Weight Distance</i>). " +
                        "Enabling the refraction model is only useful to allow volumetric clouds to render over the water surface when view from above. " +
                        "The refraction model has no effect on refractions.",
                        $"Set <i>Refraction Model</i> to <i>None</i>.",
                        MessageType.Info, target.Material
                    );
                }

                if (RenderPipelineHelper.IsHighDefinition && target.Material.HasFloat("_TransparentWritingMotionVec") && target.WriteMotionVectors != (target.Material.GetFloat("_TransparentWritingMotionVec") == 1f))
                {
                    messenger
                    (
                        $"<i>Water Renderer > Surface Renderer > Motion Vectors</i> and <i>Transparent Writes Motion Vectors</i> on <i>{target.Material}</i> do not match. ",
                        $"Either disable or enable both <i>Water Renderer > Surface Renderer > Motion Vectors</i> and <i>Transparent Writes Motion Vectors</i>",
                        MessageType.Info, target.Material
                    );
                }

                ValidateMaterialParent(target._VolumeMaterial, target.Material, messenger);
            }

            if (Object.FindObjectsByType<WaterRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length > 1)
            {
                messenger
                (
                    $"Multiple <i>{nameof(WaterRenderer)}</i> components detected in open scenes, this is not typical - usually only one <i>{nameof(WaterRenderer)}</i> is expected to be present.",
                    $"Remove extra <i>{nameof(WaterRenderer)}</i> components.",
                    MessageType.Warning, target
                );

                isValid = false;
            }

            // Water Detail Parameters
            var baseMeshDensity = target.LodResolution * 0.25f / target._GeometryDownSampleFactor;

            if (baseMeshDensity < 8)
            {
                messenger
                (
                    "Base mesh density is lower than 8. There will be visible gaps in the water surface.",
                    "Increase the <i>LOD Data Resolution</i> or decrease the <i>Geometry Down Sample Factor</i>.",
                    MessageType.Error, target
                );
            }
            else if (baseMeshDensity < 16)
            {
                messenger
                (
                    "Base mesh density is lower than 16. There will be visible transitions when traversing the water surface. ",
                    "Increase the <i>LOD Data Resolution</i> or decrease the <i>Geometry Down Sample Factor</i>.",
                    MessageType.Warning, target
                );
            }

            // We need to find hidden probes too, but do not include assets.
            if (Resources.FindObjectsOfTypeAll<ReflectionProbe>().Where(x => !EditorUtility.IsPersistent(x)).Count() > 0)
            {
                messenger
                (
                    "There are reflection probes in the scene. These can cause tiling to appear on the water surface if not set up correctly.",
                    "For reflections probes that affect the water, they will either need to cover the visible water tiles or water tiles need to ignore reflection probes (can done done with <i>Water Tile Prefab</i> field). " +
                    $"For all reflection probles that include the <i>{LayerMask.LayerToName(target.Layer)}</i> layer, make sure they are above the water surface as underwater reflections are not supported.",
                    MessageType.Info, target
                );
            }

            // Validate scene view effects options.
            if (SceneView.lastActiveSceneView != null && !Application.isPlaying)
            {
                var sceneView = SceneView.lastActiveSceneView;

                // Validate "Animated Materials".
                if (target != null && !target._ShowWaterProxyPlane && !sceneView.sceneViewState.alwaysRefresh)
                {
                    messenger
                    (
                        "<i>Animated Materials</i> is not enabled on the scene view. The water's framerate will appear low as updates are not real-time.",
                        "Enable <i>Animated Materials</i> on the scene view.",
                        MessageType.Info, target,
                        (_, _) =>
                        {
                            SceneView.lastActiveSceneView.sceneViewState.alwaysRefresh = true;
                            // Required after changing sceneViewState according to:
                            // https://docs.unity3d.com/ScriptReference/SceneView.SceneViewState.html
                            SceneView.RepaintAll();
                        }
                    );
                }

#if d_UnityPostProcessingBroken
                // Validate "Post-Processing".
                // Only check built-in renderer and Camera.main with enabled PostProcessLayer component.
                if (GraphicsSettings.currentRenderPipeline == null && Camera.main != null &&
                    Camera.main.TryGetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>(out var ppLayer)
                    && ppLayer.enabled && sceneView.sceneViewState.showImageEffects)
                {
                    messenger
                    (
                        "<i>Post Processing</i> is enabled on the scene view. " +
                        "There is a Unity bug where gizmos and grid lines will render over opaque objects. " +
                        "This has been resolved in <i>Post Processing</i> version 3.4.0.",
                        "Disable <i>Post Processing</i> on the scene view or upgrade to version 3.4.0.",
                        MessageType.Warning, target,
                        _ =>
                        {
                            sceneView.sceneViewState.showImageEffects = false;
                            // Required after changing sceneViewState according to:
                            // https://docs.unity3d.com/ScriptReference/SceneView.SceneViewState.html
                            SceneView.RepaintAll();
                        }
                    );
                }
#endif
            }

            // Validate simulation settings.
            foreach (var simulation in target.Simulations)
            {
                ExecuteValidators(simulation, messenger);
            }

            // For safety.
            if (target != null && target.Material != null)
            {
                foreach (var simulation in target.Simulations)
                {
                    ValidateSimulationAndMaterial(OptionalLod.Get(simulation.GetType()), messenger, water);
                }
            }

            if (target.PrimaryLight == null)
            {
                messenger
                (
                    "Crest needs to know which light to use as the sun light.",
                    "Please add a Directional Light to the scene.",
                    MessageType.Warning, target
                );
            }

            if (target.Viewer == null)
            {
                messenger
                (
                    "Crest needs to know which camera to use as the main camera.",
                    $"Either tag a camera as <i>Main Camera</i> or assign the camera to the {nameof(WaterRenderer)}.",
                    MessageType.Error, target
                );

                isValid = false;
            }

#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                var hdAsset = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset;
                var mvs = hdAsset.currentPlatformRenderPipelineSettings.supportMotionVectors;

                // Only check the RP asset for now.
                if (mvs != water.WriteMotionVectors)
                {
                    messenger
                    (
                        $"Motion Vectors are{(mvs ? "" : " not")} enabled in the HD render pipeline asset, but <i>Water Renderer > Surface Renderer > Motion Vectors</i> is{(mvs ? " not" : "")}. " +
                        "Both need to be enabled for motion vectors to work, or both should be disabled to save resources. " +
                        "This can safely be ignored if the setup is intentional.",
                        "Enable or disable both.",
                        MessageType.Info, target
                    );
                }
            }
#endif // d_UnityHDRP

#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal && target.Viewer != null)
            {
                var data = target.Viewer.GetUniversalAdditionalCameraData();

                // Type is internal.
                if (data != null && data.scriptableRenderer.GetType().Name == "Renderer2D")
                {
                    messenger
                    (
                        "Crest does not support 2D rendering.",
                        "Please choose a 3D template.",
                        MessageType.Error, target
                    );

                    isValid = false;
                }
            }
#endif // d_UnityURP

            if (!RenderPipelineHelper.IsHighDefinition && target.Material != null)
            {
                if (!target.AllowRenderQueueSorting && !System.Enum.IsDefined(typeof(RenderQueue), target.Material.renderQueue))
                {
                    var field = nameof(WaterRenderer.AllowRenderQueueSorting).Pretty().Italic();
                    messenger
                    (
                        $"The render queue has a sub-sort applied, but {field} is not enabled. Sub-sorting will not work.",
                        $"Enable {field}.",
                        MessageType.Warning, target
                    );
                }
            }

            return isValid;
        }

        [Validator(typeof(WaterBody))]
        static bool Validate(WaterBody target, ShowMessage messenger)
        {
            var isValid = true;

            if (Object.FindObjectsByType<WaterRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                messenger
                (
                    $"Water body <i>{target.gameObject.name}</i> requires an water renderer component to be present.",
                    $"Create a separate GameObject and add an <i>{nameof(WaterRenderer)}</i> component to it.",
                    MessageType.Error, target
                );

                isValid = false;
            }

            if (Mathf.Abs(target.transform.lossyScale.x) < 2f && Mathf.Abs(target.transform.lossyScale.z) < 2f)
            {
                messenger
                (
                    $"Water body {target.gameObject.name} has a very small size (the size is set by the X & Z scale of its transform), and will be a very small body of water.",
                    "Increase X & Z scale on water body transform (or parents).",
                    MessageType.Error, target
                );

                isValid = false;
            }

            if (target._Material != null)
            {
                isValid = ValidateWaterMaterial(target, messenger, Water, target._Material) && isValid;
                ValidateMaterialParent(target._BelowSurfaceMaterial, target._Material, messenger);
            }

            isValid = isValid && ValidateNoRotation(target, target.transform, messenger);

            return isValid;
        }


        /// <summary>
        /// Does validation for a feature on the water component and on the material
        /// </summary>
        internal static bool ValidateLod(OptionalLod target, ShowMessage messenger, WaterRenderer water, string dependent = null)
        {
            var isValid = true;

            if (target == null || water == null)
            {
                return isValid;
            }

            var simulation = target.GetLod(water);

            var dependentClause = ".";

            if (dependent != null)
            {
                dependentClause = $", as {dependent} needs it.";
            }

            if (!simulation._Enabled)
            {
                messenger
                (
                    $"<i>{target.PropertyLabel}</i> must be enabled on the <i>{nameof(WaterRenderer)}</i> component{dependentClause}",
                    $"Enable <i>Simulations > {target.PropertyLabel} > Enabled</i> on the <i>{nameof(WaterRenderer)}</i> component.",
                    MessageType.Error, water,
                    (_, y) =>
                    {
                        y.boolValue = true;
                        if (Water.Active)
                        {
                            // ApplyModifiedProperties is called outside of this method but need it for next
                            // call. Then restore so ApplyModifiedProperties check works to add undo entry.
                            simulation._Enabled = true;
                            simulation.Initialize();
                            simulation._Enabled = false;
                        }
                    },
                    $"{target.PropertyName}.{nameof(Lod._Enabled)}"
                );

                isValid = false;
            }

            var material = water.Material;

            if (target.HasMaterialToggle && material != null)
            {
                if (material.HasProperty(target.MaterialProperty) && material.GetFloat(target.MaterialProperty) != 1f)
                {
                    ShowMaterialValidationMessage(target, material, messenger);
                    isValid = false;
                }
            }

            if (target.Dependency != null)
            {
                ValidateLod(OptionalLod.Get(target.Dependency), messenger, water, dependent);
            }

            return isValid;
        }

        static bool ValidateSignedDistanceFieldsLod(ShowMessage messenger, WaterRenderer water, string feature)
        {
            var isValid = true;

            if (water != null && !water.DepthLod.EnableSignedDistanceFields)
            {
                messenger
                (
                    $"{feature} requires <i>Signed Distance Fields</i> to be enabled on the Water Depth Simulation.",
                    "Enable <i>Signed Distance Fields</i>",
                    MessageType.Error, water,
                    (_, y) => y.boolValue = true,
                    $"{nameof(WaterRenderer._DepthLod)}.{nameof(DepthLod._EnableSignedDistanceFields)}"
                );

                isValid = false;
            }

            return isValid;
        }

        static void ShowMaterialValidationMessage(OptionalLod target, Material material, ShowMessage messenger)
        {
            messenger
            (
                $"{target.PropertyLabel} is not enabled (<i>{target.MaterialPropertyPath}</i>) on the water material and will not be visible.",
                $"Enable <i>{target.PropertyLabel}</i> on the material currently assigned to the <i>{nameof(WaterRenderer)}</i> component.",
                MessageType.Error, material,
                (material, _) => FixSetMaterialOptionEnabled(material, target.MaterialKeyword, target.MaterialProperty, true)
            );
        }

        static bool ValidateSimulationAndMaterial(OptionalLod target, ShowMessage messenger, WaterRenderer water)
        {
            if (target == null)
            {
                return true;
            }

            if (!target.HasMaterialToggle)
            {
                return true;
            }

            // These checks are not necessary for our material but there may be custom materials.
            if (!water.Material.HasProperty(target.MaterialProperty))
            {
                return true;
            }

            var feature = target.GetLod(water);

            // There is only a problem if there is a mismatch.
            if (feature.Enabled == (water.Material.GetFloat(target.MaterialProperty) == 1f))
            {
                return true;
            }

            if (feature.Enabled)
            {
                ShowMaterialValidationMessage(target, water.Material, messenger);
            }
            else if (messenger != DebugLog)
            {
                messenger
                (
                    $"The <i>{target.PropertyLabel}</i> feature is disabled on the <i>{nameof(WaterRenderer)}</i> but is enabled on the water material.",
                    $"If this is not intentional, either enable <i>{target.PropertyLabel}</i> on the <i>{nameof(WaterRenderer)}</i> to turn it on, or disable <i>{target.MaterialPropertyPath}</i> on the water material to save performance.",
                    MessageType.Warning, water
                );
            }

            return false;
        }

        [Validator(typeof(ShapeWaves))]
        static bool Validate(ShapeWaves target, ShowMessage messenger)
        {
            var isValid = true;

            var water = Object.FindAnyObjectByType<WaterRenderer>(FindObjectsInactive.Include);

            if (!target.OverrideGlobalWindSpeed && water != null && water.WindSpeedKPH < WaterRenderer.k_MaximumWindSpeedKPH)
            {
                messenger
                (
                    $"The wave spectrum is limited by the <i>Global Wind Speed</i> on the <i>Water Renderer</i> to {water.WindSpeedKPH} KPH.",
                    $"If you want fully developed waves, either override the wind speed on this component or increase the <i>Global Wind Speed</i>.",
                    MessageType.Info
                );
            }

            if (target.Blend == LodInputBlend.AlphaClip && target.Mode is not LodInputMode.Texture or LodInputMode.Paint)
            {
                messenger
                (
                    $"Only {LodInputMode.Texture} mode supports {nameof(LodInputBlend.AlphaClip)}.",
                    $"Change Blend to {nameof(LodInputBlend.Alpha)}.",
                    MessageType.Error, target,
                    (_, y) => y.enumValueIndex = (int)LodInputBlend.Alpha,
                    nameof(ShapeWaves._Blend)
                );
            }

            if (Water != null)
            {
                isValid &= ValidateLod(OptionalLod.Get(typeof(AnimatedWavesLod)), messenger, Water);
            }

            return isValid;
        }

        [Validator(typeof(SphereWaterInteraction))]
        static bool Validate(SphereWaterInteraction target, ShowMessage messenger)
        {
            var isValid = true;

            // Validate require water feature.
            if (Water != null)
            {
                isValid &= ValidateLod(OptionalLod.Get(typeof(DynamicWavesLod)), messenger, Water);
            }

            return isValid;
        }

        [Validator(typeof(WatertightHull))]
        static bool Validate(WatertightHull target, ShowMessage messenger)
        {
            var isValid = true;

            // Validate require water feature.
            if (Water != null)
            {
                isValid &= !target.UsesClip || ValidateLod(OptionalLod.Get(typeof(ClipLod)), messenger, Water);
                isValid &= !target.UsesDisplacement || ValidateLod(OptionalLod.Get(typeof(AnimatedWavesLod)), messenger, Water);
                isValid &= !target.UsesDisplacement || ValidateCollisionLayer(CollisionLayer.AfterDynamicWaves, target, messenger, "mode", target.Mode, required: true);
            }

            return isValid;
        }

        internal static void FixSetCollisionSourceToCompute(SerializedObject _, SerializedProperty property)
        {
            if (Water != null)
            {
                property.enumValueIndex = (int)CollisionSource.GPU;
            }
        }

        [Validator(typeof(FloatingObject))]
        static bool Validate(FloatingObject target, ShowMessage messenger)
        {
            var isValid = true;

            isValid &= ValidateComponent(target, messenger, target.RigidBody);

            if (Water == null)
            {
                return isValid;
            }

            isValid &= ValidateCollisionLayer(target.Layer, target, messenger, "layer", target.Layer, required: false);
            isValid &= ValidateCollisionSource(target, messenger);

            return isValid;
        }

        [Validator(typeof(CollisionAreaVisualizer))]
        static bool Validate(CollisionAreaVisualizer target, ShowMessage messenger)
        {
            var isValid = true;

            if (Water == null)
            {
                return isValid;
            }

            isValid &= ValidateCollisionLayer(target._Layer, target, messenger, "layer", target._Layer, required: false);
            isValid &= ValidateCollisionSource(target, messenger);

            return isValid;
        }

        [Validator(typeof(Controller))]
        static bool Validate(Controller target, ShowMessage messenger)
        {
            var isValid = true;

            isValid &= ValidateComponent(target, messenger, target.Control);
            isValid &= ValidateComponent(target, messenger, target.FloatingObject);

            isValid &= isValid && target.TryGetComponent(out FloatingObject fo) && fo.RigidBody != null;

            return isValid;
        }

        [Validator(typeof(LodInput))]
        static bool Validate(LodInput target, ShowMessage messenger)
        {
            var isValid = true;

            var isDataInput = target.Mode is LodInputMode.Spline or LodInputMode.Texture or LodInputMode.Renderer or LodInputMode.Paint;

            if (isDataInput)
            {
                // Find the type associated with the input type and mode.
                var self = target.GetType();
                var types = TypeCache.GetTypesWithAttribute<ForLodInput>();
                System.Type type = null;
                foreach (var t in types)
                {
                    var attributes = t.GetCustomAttributes<ForLodInput>();
                    foreach (var attribute in attributes)
                    {
                        if (!attribute._Type.IsAssignableFrom(self)) continue;
                        if (attribute._Mode != target.Mode) continue;
                        type = t;
                        goto exit;
                    }
                }

            exit:
                isValid = type != null;

#if !d_CrestPaint
                if (!isValid && target.Mode == LodInputMode.Paint)
                {
                    messenger
                    (
                        "Missing the <i>Crest: Paint</i> package.",
                        $"Install the missing package or select a valid <i>Input Mode</i> such as {target.DefaultMode} to use this input.",
                        MessageType.Error,
                        target,
                        (_, y) => y.enumValueIndex = (int)target.DefaultMode,
                        nameof(target.Mode)
                    );

                    return isValid;
                }
#endif

#if !d_CrestSpline
                if (!isValid && target.Mode == LodInputMode.Spline)
                {
                    messenger
                    (
                        "Missing the <i>Crest: Spline</i> package.",
                        $"Install the missing package or select a valid <i>Input Mode</i> such as {target.DefaultMode} to use this input.",
                        MessageType.Error,
                        target,
                        (_, y) => y.enumValueIndex = (int)target.DefaultMode,
                        nameof(target.Mode)
                    );

                    return isValid;
                }
#endif

                if (!isValid)
                {
                    messenger
                    (
                        "Invalid or unset <i>Input Mode</i> setting.",
                        $"Select a valid <i>Input Mode</i> such as {target.DefaultMode} to use this input.",
                        MessageType.Error,
                        target,
                        (_, y) => y.enumValueIndex = (int)target.DefaultMode,
                        nameof(target._Mode)
                    );

                    return isValid;
                }

                isValid = target.Data != null;

                if (!isValid)
                {
                    var isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(target);
                    messenger
                    (
                        "Missing internal data or data type was renamed.",
                        isPrefabInstance ? "Repair the component in the prefab." : "Repair component.",
                        MessageType.Error,
                        target,
                        isPrefabInstance ? null : (_, _) =>
                        {
                            Undo.RecordObject(target, "Repair");
                            target.ChangeMode(target.Mode);
                            EditorUtility.SetDirty(target);
                        }
                    );

                    return isValid;
                }

                isValid = target.Data.GetType() == type;

                // This might happen if scripting is used.
                if (!isValid)
                {
                    messenger
                    (
                        $"Instance set to <i>{nameof(LodInput.Data)}</i> as incorrect type.",
                        "Set the correct instance type.",
                        MessageType.Error,
                        target,
                        (_, _) =>
                        {
                            Undo.RecordObject(target, "Repair");
                            target.ChangeMode(target.Mode);
                            EditorUtility.SetDirty(target);
                        }
                    );

                    return isValid;
                }
            }

            isValid &= ValidateFilteredChoice((int)target.Blend, "_Blend", target, messenger);

            // Validate that any water feature required for this input is enabled, if any
            if (Water != null)
            {
                isValid &= ValidateLod(OptionalLod.Get(target.GetType()), messenger, Water);
            }

            return isValid;
        }

        [Validator(typeof(DepthProbe))]
        static bool Validate(DepthProbe target, ShowMessage messenger)
        {
            var isValid = true;

            var camera = target._Camera;
            if (camera != null && camera.targetTexture != null && target.RealtimeTexture != null)
            {
                if (target.Outdated)
                {
                    messenger
                    (
                        "<i>Depth Probe</i> is outdated.",
                        "Click <i>Populate</i> or re-bake the probe to bring the probe up-to-date with component changes.",
                        MessageType.Warning, target,
                        (_, _) => target.Populate()
                    );
                }
            }

            if (target.Type == DepthProbeMode.Baked)
            {
                messenger
                (
                    "To change any read-only settings, switch back to real-time, adjust settings, and re-bake.",
                    "",
                    MessageType.Info, target
                );

                if (target.SavedTexture == null)
                {
                    messenger
                    (
                        "Depth probe type is <i>Baked</i> but no saved probe data is provided.",
                        "Assign a saved probe asset.",
                        MessageType.Error, target
                    );

                    isValid = false;
                }
            }
            else
            {
                if (target._Layers == 0)
                {
                    messenger
                    (
                        "No layers specified for rendering into depth probe.",
                        "Specify one or may layers using the Layers field.",
                        MessageType.Error, target
                    );

                    isValid = false;
                }

                if (target._Debug._ForceAlwaysUpdateDebug)
                {
                    messenger
                    (
                        $"<i>Force Always Update Debug</i> option is enabled on depth probe <i>{target.gameObject.name}</i>, which means it will render every frame instead of running from the probe.",
                        "Disable the <i>Force Always Update Debug</i> option.",
                        MessageType.Warning, target,
                        (_, y) => y.boolValue = false,
                        $"{nameof(DepthProbe._Debug)}.{nameof(DepthProbe._Debug._ForceAlwaysUpdateDebug)}"
                    );
                }

                if (target._Resolution < 4)
                {
                    messenger
                    (
                        $"Probe resolution {target._Resolution} is very low, which may not be intentional.",
                        "Increase the probe resolution.",
                        MessageType.Error, target
                    );

                    isValid = false;
                }

                if (!Mathf.Approximately(target.Scale.x, target.Scale.y))
                {
                    messenger
                    (
                        $"The <i>{nameof(DepthProbe)}</i> in real-time only supports a uniform scale for X and Z. " +
                        "These values currently do not match. " +
                        $"Its current scale in the hierarchy is: X = {target.Scale.x} Z = {target.Scale.y}.",
                        "Ensure the X & Z scale values are equal on this object and all parents in the hierarchy.",
                        MessageType.Error, target
                    );

                    isValid = false;
                }

                // We used to test if nothing is present that would render into the probe, but these could probably come from other scenes.
            }

            if (!target.Managed && target.transform.lossyScale.XZ().magnitude < 5f)
            {
                messenger
                (
                    $"<i>{nameof(DepthProbe)}</i> transform scale is small and will capture a small area of the world. The scale sets the size of the area that will be probed, and this probe is set to render a very small area.",
                    "Increase the X & Z scale to increase the size of the probe.",
                    MessageType.Warning, target
                );

                isValid = false;
            }

            if (!target.Managed && target.transform.lossyScale.y <= 0f)
            {
                messenger
                (
                    $"<i>{nameof(DepthProbe)}</i> scale is zero or negative. Y should be set to 1.0, but can be other values providing it is greater than zero. Its current scale in the hierarchy is {target.transform.lossyScale.y}.",
                    "Set the Y scale to 1.0.",
                    MessageType.Error, target
                );

                isValid = false;
            }

#if d_UnityURP
#if !UNITY_6000_0_OR_NEWER
#if UNITY_2022_3_OR_NEWER
            if (int.Parse(Application.unityVersion.Substring(7, 2)) < 23)
            {
                // Asset based validation.
                foreach (var asset in GraphicsSettings.allConfiguredRenderPipelines)
                {
                    if (asset is UniversalRenderPipelineAsset urpAsset)
                    {
                        var urpRenderers = Helpers.UniversalRendererData(urpAsset);

                        foreach (var renderer in urpRenderers)
                        {
                            var urpRenderer = (UniversalRendererData)renderer;

                            if (urpRenderer.depthPrimingMode != DepthPrimingMode.Disabled)
                            {
                                messenger
                                (
                                    $"<i>{nameof(DepthPrimingMode)}</i> is not set to <i>{nameof(DepthPrimingMode.Disabled)}</i>. " +
                                    $"This can cause the <i>{nameof(DepthProbe)}</i> not to work. " +
                                    $"Unity fixed this in 2022.3.23f1.",
                                    $"If you are experiencing problems, disable depth priming or upgrade Unity.",
                                    MessageType.Info, urpRenderer
                                );
                            }

                            foreach (var feature in renderer.rendererFeatures)
                            {
                                if (feature.GetType().Name == "ScreenSpaceAmbientOcclusion" && feature.isActive)
                                {
                                    messenger
                                    (
                                        $"<i>ScreenSpaceAmbientOcclusion</i> is is active. " +
                                        $"This can cause the <i>{nameof(DepthProbe)}</i> not to work. " +
                                        $"Unity fixed this in 2022.3.23f1.",
                                        $"If you are experiencing problems, disable SSAO or upgrade Unity.",
                                        MessageType.Info, urpRenderer
                                    );
                                }
                            }
                        }
                    }
                }
            }
#endif
#endif
#endif

            // Check that there are no renderers in descendants.
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                foreach (var renderer in renderers)
                {
                    messenger
                    (
                        "It is not expected that a depth probe object has a Renderer component in its hierarchy." +
                        "The probe is typically attached to an empty GameObject. Please refer to the example content.",
                        "Remove the Renderer component from this object or its children.",
                        MessageType.Warning, renderer
                    );

                    // Reporting only one renderer at a time will be enough to avoid overwhelming user and UI.
                    break;
                }

                isValid = false;
            }

            var water = Water;

            // Validate require water feature.
            if (water != null)
            {
                isValid = isValid && ValidateLod(OptionalLod.Get(typeof(DepthLod)), messenger, water);

                if (!water._DepthLod._EnableSignedDistanceFields && target._GenerateSignedDistanceField)
                {
                    isValid = isValid && ValidateSignedDistanceFieldsLod(messenger, water, "Generate Signed Distance Field");
                }
            }


            return isValid;
        }

        [Validator(typeof(QueryEvents))]
        static bool Validate(QueryEvents target, ShowMessage messenger)
        {
            var isValid = true;
            var water = Water;

            if (!target._DistanceFromEdge.IsEmpty())
            {
                isValid = isValid && ValidateLod(OptionalLod.Get(typeof(DepthLod)), messenger, water);
                isValid = isValid && ValidateSignedDistanceFieldsLod(messenger, water, "Distance From Edge");
            }

            if (!target._DistanceFromSurface.IsEmpty())
            {
                isValid &= ValidateCollisionLayer(target._Layer, target, messenger, "layer", target._Layer, required: false);
                isValid &= ValidateCollisionSource(target, messenger);
            }

            return isValid;
        }

        [Validator(typeof(FoamLodSettings))]
        static bool Validate(FoamLodSettings target, ShowMessage messenger)
        {
            var isValid = true;

            if (Water == null)
            {
                return isValid;
            }

            if (target.FilterWaves > Water.LodLevels - 2)
            {
                messenger
                (
                    "<i>Filter Waves</i> is higher than the recommended maximum (LOD count - 2). There will be no whitecaps.",
                    "Reduce <i>Filter Waves</i>.",
                    MessageType.Warning, target
                );
            }

            return isValid;
        }

        [Validator(typeof(Lod))]
        static bool Validate(Lod target, ShowMessage messenger)
        {
            var isValid = true;

            if (!target._Enabled)
            {
                return isValid;
            }

            var optional = OptionalLod.Get(target.GetType());

            if (Water != null && optional.Dependency != null)
            {
                isValid &= ValidateLod(OptionalLod.Get(optional.Dependency), messenger, Water, target.Name);
            }

            return isValid;
        }

        [Validator(typeof(AnimatedWavesLod))]
        static bool Validate(AnimatedWavesLod target, ShowMessage messenger)
        {
            var isValid = true;

#if !d_CrestCPUQueries
            if (target.CollisionSource == CollisionSource.CPU)
            {
                messenger
                (
                    "Collision Source is set to CPU but the <i>CPU Queries</i> package is not installed.",
                    "Install the <i>CPU Queries</i> package or switch to GPU queries.",
                    MessageType.Warning, target.Water,
                    FixSetCollisionSourceToCompute
                );
            }
#endif

            if (target.CollisionSource == CollisionSource.None)
            {
                messenger
                (
                    "Collision Source in Water Renderer is set to None. The floating objects in the scene will use a flat horizontal plane.",
                    "Set collision source to GPU.",
                    MessageType.Warning, target.Water,
                    FixSetCollisionSourceToCompute,
                    $"{nameof(WaterRenderer._AnimatedWavesLod)}.{nameof(AnimatedWavesLod._CollisionSource)}"

                );
            }

            return isValid;
        }

        [Validator(typeof(ScatteringLod))]
        static bool Validate(ScatteringLod target, ShowMessage messenger)
        {
            var isValid = true;

            var water = Water;

            if (!target.Enabled)
            {
                return isValid;
            }

            if (target._ShorelineColorSource != ShorelineVolumeColorSource.None)
            {
                if (!water._DepthLod._Enabled)
                {
                    ShowDependentPropertyMessage
                    (
                        "Shoreline Scattering",
                        "Water Depth",
                        $"{nameof(WaterRenderer._DepthLod)}.{nameof(Lod._Enabled)}",
                        messenger,
                        water
                    );
                }
                else if (target._ShorelineColorSource == ShorelineVolumeColorSource.Distance && !water._DepthLod._EnableSignedDistanceFields)
                {
                    ShowDependentPropertyMessage
                    (
                        "Shoreline Distance Scattering",
                        "Signed Distance Fields",
                        $"{nameof(WaterRenderer._DepthLod)}.{nameof(WaterRenderer._DepthLod._EnableSignedDistanceFields)}",
                        messenger,
                        water
                    );
                }
            }

            return isValid;
        }

        [Validator(typeof(CutsceneTimeProvider))]
        static bool Validate(CutsceneTimeProvider target, ShowMessage messenger)
        {
            var isValid = true;

            var water = Water;
            if (water == null)
            {
                messenger
                (
                    $"No water present. {nameof(CutsceneTimeProvider)} will have no effect.",
                    "", MessageType.Warning
                );

                isValid = false;
            }

#if d_ModuleUnityDirector
            if (target._PlayableDirector == null)
            {
                messenger
                (
                    $"No {nameof(UnityEngine.Playables.PlayableDirector)} component assigned. {nameof(CutsceneTimeProvider)} will have no effect.",
                    $"Add a {nameof(UnityEngine.Playables.PlayableDirector)}",
                    MessageType.Error
                );

                isValid = false;
            }
#else
            messenger
            (
                $"This component requires the com.unity.modules.director built-in module to function.",
                $"Enable the com.unity.modules.director built-in module.",
                MessageType.Error
            );

            isValid = false;
#endif

            return isValid;
        }

        static bool ValidateWaterMaterial(Object target, ShowMessage messenger, WaterRenderer water, Material material)
        {
            var isValid = true;

            // TODO: We could be even more granular with what needs this property.
            if (water._Underwater._Enabled && !material.HasVector(WaterRenderer.ShaderIDs.s_Absorption))
            {
                messenger
                (
                    $"Material <i>{material.name}</i> does not have <i>Crest Absorption</i> property. " +
                    "Several features require absorption like underwater culling and lighting.",
                    $"Assign a valid water material.",
                    MessageType.Warning, target
                );
            }

            return isValid;
        }

        static bool ValidateMaterialParent(Material child, Material parent, ShowMessage messenger)
        {
            var isValid = true;

            if (child != null && child.parent != parent)
            {
                messenger
                (
                    $"The <i>{child}</i> does not have <i>{parent}</i> as a parent. " +
                    "Linking these materials is typically how these are used to avoid trying to keep properties in sync.",
                    $"Parent <i>{parent}</i> to <i>{child}</i>.",
                    MessageType.Info, parent,
                    (_, _) =>
                    {
                        Undo.RecordObject(child, "Assign parent");
                        child.parent = parent;
                    }
                );
            }

            return isValid;
        }

        static bool ValidateComponent<T, C>(T target, ShowMessage messenger, C @object)
            where T : Component
            where C : Component
        {
            var isValid = true;

            if (@object == null && !target.gameObject.TryGetComponent<C>(out _))
            {
                messenger
                (
                    $"{typeof(T).Name} requires a {typeof(C).Name} to be set or present on same object.",
                    $"Set the {typeof(C).Name} property or add a {typeof(C).Name}.",
                    MessageType.Error
                );

                isValid = false;
            }

            return isValid;
        }

        static bool ValidateCollisionLayer(CollisionLayer layer, Object target, ShowMessage messenger, string label, object value, bool required)
        {
            if (Water == null)
            {
                return true;
            }

            var layers = Water.AnimatedWavesLod._CollisionLayers;

            if (layer == CollisionLayer.Everything)
            {
                return true;
            }

            var flag = (CollisionLayers)((int)layer << 1);

            if (!layers.HasFlag(flag))
            {
                var fix = $"Enable the {flag} layer on the {nameof(WaterRenderer)}.";
                if (!required) fix += " You can safely ignore this warning.";

                messenger
                (
                    $"The {value} {label} requires the {flag} layer which is not enabled.",
                    fix,
                    required ? MessageType.Error : MessageType.Warning, messenger == DebugLog ? target : Water,
                    (_, y) => y.intValue = (int)(layers | flag),
                    $"{nameof(WaterRenderer._AnimatedWavesLod)}.{nameof(WaterRenderer._AnimatedWavesLod._CollisionLayers)}"
                );

                return !required;
            }

            return true;
        }

        static bool ValidateCollisionSource(Object target, ShowMessage messenger)
        {
            if (Water == null)
            {
                return true;
            }

            if (Water._AnimatedWavesLod.CollisionSource == CollisionSource.None)
            {
                messenger
                (
                    "<i>Collision Source</i> on the <i>Water Renderer</i> is set to <i>None</i>. The floating objects in the scene will use a flat horizontal plane.",
                    "Set the <i>Collision Source</i> to <i>GPU</i> to incorporate waves into physics.",
                    MessageType.Warning, Water,
                    FixSetCollisionSourceToCompute
                );
            }

            return true;
        }

        static bool ValidateFilteredChoice(int choice, string property, Object target, ShowMessage messenger)
        {
            var filter = target
                .GetType()
                .GetCustomAttributes<FilterEnum>(inherit: true)
                .FirstOrDefault(x => x._Property == property);

            if (filter?._Values.Contains(choice) == false)
            {
                var label = property[1..];

                messenger
                (
                    $"The {label} property is invalid.",
                    $"Choose a correct {label} property.",
                    MessageType.Error,
                    target
                );

                return false;
            }

            return true;
        }

        static void ShowDependentPropertyMessage(string dependentLabel, string dependencyLabel, string dependencyPropertyPath, ShowMessage messenger, Object dependencyContext)
        {
            messenger
            (
                $"{dependencyLabel} is not enabled, but {dependentLabel} requires it.",
                $"Enable {dependencyLabel}.",
                MessageType.Warning, dependencyContext,
                (_, y) => y.boolValue = true,
                dependencyPropertyPath
            );
        }
    }
}
