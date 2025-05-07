// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Contains extensions to support scripting.
    /// </summary>
    public static partial class _Extensions
    {
        /// <summary>
        /// AddComponent that is compatible with Crest inputs.
        /// </summary>
        /// <typeparam name="T">The input type.</typeparam>
        /// <param name="gameObject">The game object to add the input to.</param>
        /// <param name="mode">The input mode. Not all inputs support all modes. Refer to the UI as to what input supports what mode.</param>
        /// <returns>The newly created input component setup with the provided mode.</returns>
        public static T AddComponent<T>(this GameObject gameObject, LodInputMode mode)
            where T : LodInput
        {
            var input = gameObject.AddComponent<T>();
            input._Mode = mode;
            // Not all modes have associated data.
            if (mode is not LodInputMode.Global or LodInputMode.Primitive or LodInputMode.Unset) AddData(input, mode);
            input.InferBlend();
            return input;
        }

        static void AddData<T>(this LodInput input, LodInputMode mode) where T : LodInputData, new()
        {
            input.Data = new T();
            input.Data._Input = input;
        }
    }
}
