// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Constructs a custom link to Crest's documentation for the help URL button.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, AllowMultiple = false)]
    sealed class HelpURL : HelpURLAttribute
    {
        public HelpURL(string path = "") : base(GetPageLink(path))
        {
            // Blank.
        }

        public static string GetPageLink(string path)
        {
            return "https://docs.crest.waveharmonic.com/" + path;
        }
    }
}
