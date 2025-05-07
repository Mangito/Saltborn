// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using UnityEngine;

namespace WaveHarmonic.Crest.Utility
{
    /// <summary>
    /// Circular buffer to store a multiple sets of data.
    /// </summary>
    sealed class BufferedData<T>
    {
        readonly T[] _Buffers;
        int _CurrentFrameIndex;

        public T Current { get => _Buffers[_CurrentFrameIndex]; set => _Buffers[_CurrentFrameIndex] = value; }
        public int Size => _Buffers.Length;

        public BufferedData(int size, Func<T> initialize)
        {
            _Buffers = new T[size];

            for (var i = 0; i < size; i++)
            {
                _Buffers[i] = initialize();
            }
        }

        public T Previous(int framesBack)
        {
            Debug.Assert(framesBack >= 0 && framesBack < _Buffers.Length);
            return _Buffers[(_CurrentFrameIndex - framesBack + _Buffers.Length) % _Buffers.Length];
        }

        public void Flip()
        {
            _CurrentFrameIndex = (_CurrentFrameIndex + 1) % _Buffers.Length;
        }

        public void RunLambda(Action<T> lambda)
        {
            foreach (var buffer in _Buffers)
            {
                lambda(buffer);
            }
        }
    }
}
