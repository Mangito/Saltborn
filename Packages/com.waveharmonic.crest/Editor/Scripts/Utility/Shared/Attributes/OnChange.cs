// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;

namespace WaveHarmonic.Crest
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    sealed class OnChange : Attribute
    {
        public Type Type { get; }
        public bool SkipIfInactive { get; }

        /// <summary>
        /// Register an instance method as an OnChange handler.
        /// </summary>
        public OnChange(bool skipIfInactive = true)
        {
            SkipIfInactive = skipIfInactive;
        }

        /// <summary>
        /// Register a static method as an OnChange handler.
        /// </summary>
        /// <param name="type">The type to target.</param>
        /// <param name="skipIfInactive">Skip this handler if component is inactive.</param>
        public OnChange(Type type, bool skipIfInactive = true)
        {
            Type = type;
            SkipIfInactive = skipIfInactive;
        }
    }
}
