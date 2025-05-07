// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WaveHarmonic.Crest.Samples.Editor")]
[assembly: InternalsVisibleTo("WaveHarmonic.Crest.CPUQueries.Editor")]
[assembly: InternalsVisibleTo("WaveHarmonic.Crest.Paint.Editor")]
[assembly: InternalsVisibleTo("WaveHarmonic.Crest.ShallowWater.Editor")]
[assembly: InternalsVisibleTo("WaveHarmonic.Crest.Splines.Editor")]
[assembly: InternalsVisibleTo("WaveHarmonic.Crest.Watercraft.Editor")]
[assembly: InternalsVisibleTo("WaveHarmonic.Crest.Whirlpool.Editor")]

// Define empty namespaces for when assemblies are not present.
namespace UnityEditor.Rendering.HighDefinition { }
namespace UnityEngine.Rendering.HighDefinition { }
namespace UnityEngine.Rendering.Universal { }
namespace WaveHarmonic.Crest.Paint { }
namespace WaveHarmonic.Crest.Splines { }
