// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    static class TextureArrayHelpers
    {
        const int k_SmallTextureSize = 4;

        public static Texture2D CreateTexture2D(Color color, TextureFormat format)
        {
            var texture = new Texture2D(k_SmallTextureSize, k_SmallTextureSize, format, false, false);
            var pixels = new Color[texture.height * texture.width];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Texture2DArray CreateTexture2DArray(Texture2D texture, int depth)
        {
            var array = new Texture2DArray(
                k_SmallTextureSize, k_SmallTextureSize,
                depth,
                texture.format,
                false,
                false
            );

            for (var textureArrayIndex = 0; textureArrayIndex < array.depth; textureArrayIndex++)
            {
                Graphics.CopyTexture(texture, 0, 0, array, textureArrayIndex, 0);
            }

            array.Apply();

            return array;
        }
    }
}
