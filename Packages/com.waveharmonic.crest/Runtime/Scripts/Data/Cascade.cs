// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    readonly struct Cascade
    {
        public readonly Vector2 _SnappedPosition;
        public readonly float _Texel;
        public readonly int _Resolution;
        public readonly Vector4 Packed => new(_SnappedPosition.x, _SnappedPosition.y, _Texel, 0f);

        public Cascade(Vector2 snapped, float texel, int resolution)
        {
            _SnappedPosition = snapped;
            _Texel = texel;
            _Resolution = resolution;
        }

        public readonly Rect TexelRect
        {
            get
            {
                var w = _Texel * _Resolution;
                return new(_SnappedPosition.x - w / 2f, _SnappedPosition.y - w / 2f, w, w);
            }
        }
    }
}
