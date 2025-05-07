// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
#if !d_CrestPortals
    namespace Portals
    {
        // Dummy script to keep serializer from complaining.
        [System.Serializable]
        public sealed class PortalRenderer { }
    }
#endif

    partial class WaterRenderer
    {
        internal const float k_MaximumWindSpeedKPH = 150f;

        [@Space(1, isAlwaysVisible: true)]

        [@Group("General", Group.Style.Accordian)]

        [Tooltip("The camera which drives the water data.\n\nSetting this is optional. Defaults to the main camera.")]
        [@GenerateAPI(Getter.Custom, name: "Viewer")]
        [@DecoratedField, SerializeField]
        Camera _Camera;

        [Tooltip("Optional provider for time.\n\nCan be used to hard-code time for automation, or provide server time. Defaults to local Unity time.")]
        [@DecoratedField, SerializeField]
        internal TimeProvider _TimeProvider;

        [Tooltip("Whether to override the automatic detection of framebuffer HDR rendering (BIRP only).\n\nRendering using HDR formats is optional, but there is no way for us to determine if HDR rendering is enabled in the Graphics Settings. We make an educated based on which platform is the target. If you see rendering issues, try disabling this.\n\n This has nothing to do with having an HDR monitor.")]
        [@Predicated(RenderPipeline.Legacy, hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _OverrideRenderHDR;

        [Tooltip("Force HDR format usage (BIRP only).\n\nIf enabled, we assume the framebuffer is an HDR format, otherwise an LDR format.")]
        [@Predicated(RenderPipeline.Legacy, hide: true)]
        [@Predicated(nameof(_OverrideRenderHDR))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _RenderHDR = true;


        [@Group("Environment", Group.Style.Accordian)]

        [Tooltip("Base wind speed in km/h.\n\nControls wave conditions. Can be overridden on Shape* components.")]
        [@Range(0, k_MaximumWindSpeedKPH, scale: 2f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _WindSpeed = 10f;

        [Tooltip("Provide your own gravity value instead of Physics.gravity.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _OverrideGravity;

        [@Label("Gravity")]
        [Tooltip("Gravity for all wave calculations.")]
        [@Predicated(nameof(_OverrideGravity))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _GravityOverride = -9.8f;

        [Tooltip("Multiplier for physics gravity.")]
        [@Range(0f, 10f)]
        [@GenerateAPI]
        [SerializeField]
        float _GravityMultiplier = 1f;

        [Tooltip("The primary light that affects the water.\n\nSetting this is optional. This should be a directional light. Defaults to RenderSettings.sun.")]
        [@GenerateAPI(Getter.Custom)]
        [@DecoratedField, SerializeField]
        Light _PrimaryLight;


        [@Group("Surface Renderer", Group.Style.Accordian)]

        [Tooltip("The water chunk renderers will have this layer.")]
        [@Layer]
        [@GenerateAPI]
        [SerializeField]
        int _Layer = 4; // Water

        [Tooltip("Material to use for the water surface.")]
        [@AttachMaterialEditor]
        [@MaterialField("Crest/Water", name: "Water", title: "Create Water Material")]
        [@GenerateAPI]
        [SerializeField]
        internal Material _Material = null;

        [Tooltip("Underwater will copy from this material if set.\n\nUseful for overriding properties for the underwater effect. To see what properties can be overriden, see the disabled properties on the underwater material. This does not affect the surface.")]
        [@AttachMaterialEditor]
        [@MaterialField("Crest/Water", name: "Water (Below)", title: "Create Water Material", parent: "_Material")]
        [@GenerateAPI]
        [SerializeField]
        internal Material _VolumeMaterial = null;

        [Tooltip("Template for water chunks as a prefab.\n\nThe only requirements are that the prefab must contain a MeshRenderer at the root and not a MeshFilter or WaterChunkRenderer. MR values will be overwritten where necessary and the prefabs are linked in edit mode.")]
        [@PrefabField(title: "Create Chunk Prefab", name: "Water Chunk")]
        [SerializeField]
        internal GameObject _ChunkTemplate;

        [@Space(10)]

        [Tooltip("Have the water surface cast shadows for albedo (both foam and custom).")]
        [@Predicated(RenderPipeline.Legacy, inverted: true, hide: true)]
        [@GenerateAPI(Getter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _CastShadows;

        [@Label("Motion Vectors")]
        [Tooltip("Whether to enable motion vector support.")]
        [@Predicated(RenderPipeline.Legacy, inverted: true, hide: true)]
#if !UNITY_6000_0_OR_NEWER
        [@Predicated(RenderPipeline.Universal, inverted: true, hide: true)]
#endif
        [@GenerateAPI(Getter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _WriteMotionVectors = true;

        [Tooltip("Whether to write the water surface depth to the depth texture (URP only).\n\nThe water surface writes to the depth buffer, but Unity does not copy it to the depth texture for post-processing effects like Depth of Field. This will copy the depth buffer to the depth texture.\n\nBe wary that it will include all transparent objects that write to depth. Furthermore, other third parties may already be doing this, and we do not check whether it is necessary to copy or not.")]
        [@Predicated(RenderPipeline.Universal, hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _WriteToDepthTexture = true;

        [@Heading("Culling")]

        [Tooltip("Whether 'Water Body' components will cull the water tiles.\n\nDisable if you want to use the 'Material Override' feature and still have an ocean.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _WaterBodyCulling = true;

        [Tooltip("How many frames to distribute the chunk bounds calculation.\n\nThe chunk bounds are calculated per frame to ensure culling is correct when using inputs that affect displacement. Some performance can be saved by distributing the load over several frames. The higher the frames, the longer it will take - lowest being instant.")]
        [@Range(1, 30, Range.Clamp.Minimum)]
        [@GenerateAPI]
        [SerializeField]
        int _TimeSliceBoundsUpdateFrameCount = 1;

        [@Heading("Advanced")]

        [Tooltip("How to handle self-intersections of the water surface.\n\nThey can be caused by choppy waves which can cause a flipped underwater effect. When not using the portals/volumes, this fix is only applied when within 2 metres of the water surface. Automatic will disable the fix if portals/volumes are used which is the recommend setting.")]
        [@DecoratedField, SerializeField]
        SurfaceSelfIntersectionFixMode _SurfaceSelfIntersectionFixMode = SurfaceSelfIntersectionFixMode.Automatic;

        [Tooltip("Whether to allow sorting using the render queue.\n\nIf you need to change the minor part of the render queue (eg +100), then enable this option. As a side effect, it will also disable the front-to-back rendering optimization for Crest. This option does not affect changing the major part of the render queue (eg AlphaTest, Transparent), as that is always allowed.\n\nRender queue sorting is required for some third-party integrations.")]
        [@Predicated(RenderPipeline.HighDefinition, inverted: true, hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _AllowRenderQueueSorting;


        [@Group("Level of Detail", Group.Style.Accordian)]

        [@Label("Scale")]
        [Tooltip("The scale the water can be (infinity for no maximum).\n\nWater is scaled horizontally with viewer height, to keep the meshing suitable for elevated viewpoints. This sets the minimum and maximum the water will be scaled. Low minimum values give lots of detail, but will limit the horizontal extents of the water detail. Increasing the minimum value can be a great performance saving for mobile as it will reduce draw calls.")]
        [@Range(0.25f, 256f, Range.Clamp.Minimum, delayed: false)]
        [@GenerateAPI]
        [SerializeField]
        Vector2 _ScaleRange = new(4f, 256f);

        [Tooltip("Drops the height for maximum water detail based on waves.\n\nThis means if there are big waves, max detail level is reached at a lower height, which can help visual range when there are very large waves and camera is at sea level.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _DropDetailHeightBasedOnWaves = 0.2f;

        [@Label("Levels")]
        [Tooltip("Number of levels of details (chunks, scales etc) to generate.\n\nThe horizontal range of the water surface doubles for each added LOD, while GPU processing time increases linearly. The higher the number, the further out detail will be. Furthermore, the higher the count, the more larger wavelengths can be filtering in queries.")]
        [@Range(2, Lod.k_MaximumSlices)]
        [@GenerateAPI(name: "LodLevels")]
        [SerializeField]
        int _Slices = 7;

        [@Label("Resolution")]
        [Tooltip("The resolution of the various water LOD data.\n\nThis includes mesh density, displacement textures, foam data, dynamic wave simulation, etc. Sets the 'detail' present in the water - larger values give more detail at increased run-time expense. This value can be overriden per LOD in their respective settings except for Animated Waves which is tied to this value.")]
        [@Range(80, 1024, Range.Clamp.Minimum, step: 16, delayed: true)]
        [@Maximum(Constants.k_MaximumTextureResolution)]
        [@WarnIfAbove(1024)]
        [@GenerateAPI(name: "LodResolution")]
        [SerializeField]
        int _Resolution = 384;

        [Tooltip("How much of the water shape gets tessellated by geometry.\n\nFor example, if set to four, every geometry quad will span 4x4 LOD data texels. a value of 2 will generate one vert per 2x2 LOD data texels. A value of 1 means a vert is generated for every LOD data texel. Larger values give lower fidelity surface shape with higher performance.")]
        [@Delayed]
        [@GenerateAPI]
        [SerializeField]
        internal int _GeometryDownSampleFactor = 2;

        [Tooltip("Applied to the extents' far vertices to make them larger.\n\nIncrease if the extents do not reach the horizon or you see the underwater effect at the horizon.")]
        [@Delayed]
        [@GenerateAPI]
        [SerializeField]
        internal float _ExtentsSizeMultiplier = 100f;

        [@Heading("Center of Detail")]

        [Tooltip("The viewpoint which drives the water detail - the center of the LOD system.\n\nSetting this is optional. Defaults to the camera.")]
        [@GenerateAPI(Getter.Custom)]
        [@DecoratedField, SerializeField]
        Transform _Viewpoint;

        [Tooltip("Also checks terrain height when determining the scale.\n\nThe scale is changed based on the viewer's height above the water surface. This can be a problem with varied water level, as the viewer may not be directly over the higher water level leading to a height difference, and thus incorrect scale.")]
        [Predicated(nameof(_Viewpoint), inverted: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _SampleTerrainHeightForScale = true;

        [Tooltip("Forces smoothing for scale changes.\n\nWhen water level varies, smoothing scale change can prevent pops when the viewer's height above water sharply changes. Smoothing is disabled when terrain sampling is enabled or the water level simulation is disabled.")]
        [Predicated(nameof(_Viewpoint), inverted: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _ForceScaleChangeSmoothing;

        [Tooltip("The distance threshold for when the viewer has considered to have teleported.\n\nThis is used to prevent popping, and for prewarming simulations. Threshold is in Unity units.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _TeleportThreshold = 100f;


        [@Group("Simulations", Group.Style.Accordian)]

        [@Label("Animated Waves")]
        [Tooltip("All waves (including Dynamic Waves) are written to this simulation.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal AnimatedWavesLod _AnimatedWavesLod = new();

        [@Label("Water Depth")]
        [Tooltip("Water depth information used for shallow water, shoreline foam, wave attenuation, among others.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal DepthLod _DepthLod = new();

        [@Label("Water Level")]
        [Tooltip("Varying water level to support water bodies at different heights and rivers to run down slopes.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal LevelLod _LevelLod = new();

        [@Label("Foam")]
        [Tooltip("Simulation of foam created in choppy water and dissipating over time.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal FoamLod _FoamLod = new();

        [@Label("Dynamic Waves")]
        [Tooltip("Dynamic waves generated from interactions with objects such as boats.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal DynamicWavesLod _DynamicWavesLod = new();

        [@Label("Flow")]
        [Tooltip("Horizontal motion of water body, akin to water currents.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal FlowLod _FlowLod = new();

        [@Label("Shadows")]
        [Tooltip("Shadow information used for lighting water.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal ShadowLod _ShadowLod = new();

        [@Label("Absorption")]
        [Tooltip("Absorption information - gives color to water.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal AbsorptionLod _AbsorptionLod = new();

        [@Label("Scattering")]
        [Tooltip("Scattering information - gives color to water.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal ScatteringLod _ScatteringLod = new();

        [@Label("Surface Clipping")]
        [Tooltip("Clip surface information for clipping the water surface.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal ClipLod _ClipLod = new();

        [@Label("Albedo / Decals")]
        [Tooltip("Albedo - a colour layer composited onto the water surface.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeReference]
        internal AlbedoLod _AlbedoLod = new();


        [@Group(isCustomFoldout: true)]

        [Tooltip("The reflection renderer.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField(isCustomFoldout: true), SerializeReference]
        internal WaterReflections _Reflections = new();


        [@Group(isCustomFoldout: true)]

        [Tooltip("The underwater renderer.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField(isCustomFoldout: true), SerializeReference]
        internal UnderwaterRenderer _Underwater = new();


#if !d_CrestPortals
        // Hide if package is not present. Fallback to dummy script.
        [HideInInspector]
#endif

        [@Group(isCustomFoldout: true)]

        [Tooltip("The portal renderer.")]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField(isCustomFoldout: true), SerializeReference]
        internal Portals.PortalRenderer _Portals = new();


        [@Group("Edit Mode", Group.Style.Accordian)]

#pragma warning disable 414
        [@DecoratedField, SerializeField]
        internal bool _ShowWaterProxyPlane;

        [Tooltip("Sets the update rate of the water system when in edit mode.\n\nCan be reduced to save power.")]
        [@Range(0f, 120f, Range.Clamp.Minimum)]
        [SerializeField]
        float _EditModeFrameRate = 30f;

        [Tooltip("Move water with Scene view camera if Scene window is focused.")]
        [@Predicated(nameof(_ShowWaterProxyPlane), true)]
        [@DecoratedField, SerializeField]
        internal bool _FollowSceneCamera = true;

        [Tooltip("Whether height queries are enabled in edit mode.")]
        [@DecoratedField, SerializeField]
        internal bool _HeightQueries = true;
#pragma warning restore 414


        [@Group("Debug", isCustomFoldout: true)]

        [@DecoratedField(isCustomFoldout: true), SerializeField]
        internal DebugFields _Debug = new();

        [System.Serializable]
        internal sealed class DebugFields
        {
            [@Space(10)]

            [Tooltip("Attach debug GUI that adds some controls and allows to visualize the water data.")]
            [@DecoratedField, SerializeField]
            public bool _AttachDebugGUI;

            [Tooltip("Show hidden objects like water chunks in the hierarchy.")]
            [@DecoratedField, SerializeField]
            public bool _ShowHiddenObjects;

#if !CREST_DEBUG
            [HideInInspector]
#endif
            [Tooltip("Water will not move with viewpoint.")]
            [@DecoratedField, SerializeField]
            public bool _DisableFollowViewpoint;

            [Tooltip("Resources are normally released in OnDestroy (except in edit mode) which avoids expensive rebuilds when toggling this component. This option moves it to OnDisable. If you need this active then please report to us.")]
            [@DecoratedField, SerializeField]
            public bool _DestroyResourcesInOnDisable;

#if CREST_DEBUG
            [Tooltip("Whether to disable chunk generation.")]
            [@DecoratedField, SerializeField]
            public bool _DisableChunks;

            [Tooltip("Whether to generate water geometry tiles uniformly (with overlaps).")]
            [@DecoratedField, SerializeField]
            public bool _UniformTiles;

            [Tooltip("Disable generating a wide strip of triangles at the outer edge to extend water to edge of view frustum.")]
            [@DecoratedField, SerializeField]
            public bool _DisableSkirt;

            [@DecoratedField, SerializeField]
            public bool _DrawLodOutline;

            [@DecoratedField, SerializeField]
            public bool _ShowDebugInformation;
#endif

            [@Heading("Scale")]

#if !CREST_DEBUG
            [HideInInspector]
#endif
            [Tooltip("Water will not move with viewpoint.")]
            [@DecoratedField, SerializeField]
            public bool _LogScaleChange;

#if !CREST_DEBUG
            [HideInInspector]
#endif
            [Tooltip("Water will not move with viewpoint.")]
            [@DecoratedField, SerializeField]
            public bool _PauseOnScaleChange;

#if !CREST_DEBUG
            [HideInInspector]
#endif
            [Tooltip("Water will not move with viewpoint.")]
            [@DecoratedField, SerializeField]
            public bool _IgnoreWavesForScaleChange;

            [@Heading("Server")]

            [Tooltip("Emulate batch mode which models running without a display (but with a GPU available). Equivalent to running standalone build with -batchmode argument.")]
            [@DecoratedField, SerializeField]
            public bool _ForceBatchMode;

            [Tooltip("Emulate running on a client without a GPU. Equivalent to running standalone with -nographics argument.")]
            [@DecoratedField, SerializeField]
            public bool _ForceNoGraphics;
        }

        [SerializeField, HideInInspector]
        internal WaterResources _Resources;
    }
}
