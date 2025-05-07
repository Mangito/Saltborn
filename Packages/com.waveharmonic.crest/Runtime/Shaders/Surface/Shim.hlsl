// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Constants for shader graph. For example, we can force shader features when we have yet to make a keyword for it in
// shader graph.

// This file must be included before all other includes. And it must be done for every node. This is due to #ifndef
// limiting includes from being evaluated once, and we cannot specify the order because shader graph does this.

#ifndef CREST_SHADERGRAPH_CONSTANTS_H
#define CREST_SHADERGRAPH_CONSTANTS_H

#ifdef UNIVERSAL_PIPELINE_CORE_INCLUDED
    #define CREST_URP 1

    #if (SHADERPASS == SHADERPASS_SHADOWCASTER)
        #define CREST_SHADOWPASS 1
    #endif

#if _SURFACE_TYPE_TRANSPARENT
    #define d_Transparent 1
#endif

#elif BUILTIN_TARGET_API
    #define CREST_BIRP 1

    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/InputsDriven.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/Common.hlsl"

#if _BUILTIN_SURFACE_TYPE_TRANSPARENT
    #define d_Transparent 1
#endif

    // SHADERPASS is currently broken:
    // https://forum.unity.com/threads/built-in-renderer-shaderpass-include-in-wrong-place.1444156/
    // #if (SHADERPASS == SHADERPASS_SHADOWCASTER)
    //     #define CREST_SHADOWPASS 1
    // #endif
#else
    // HDRP does not appear to have a reliable keyword to target.
    #define CREST_HDRP 1

#if _SURFACE_TYPE_TRANSPARENT
    #define d_Transparent 1
#endif

    #if (SHADERPASS == SHADERPASS_SHADOWS)
        #define CREST_SHADOWPASS 1
    #endif
#endif

#if defined(CREST_HDRP) && (SHADERPASS == SHADERPASS_FORWARD)
#define CREST_HDRP_FORWARD_PASS 1
#endif

#endif // CREST_SHADERGRAPH_CONSTANTS_H
