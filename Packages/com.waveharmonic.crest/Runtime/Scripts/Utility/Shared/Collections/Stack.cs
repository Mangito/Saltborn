// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace WaveHarmonic.Crest.Utility.Internal
{
    /// <summary>
    /// A less rigid stack implementation which is easier to use. Prevents duplicates.
    /// </summary>
    /// <typeparam name="T">Type to store.</typeparam>
    public sealed class Stack<T>
    {
        readonly List<T> _Items = new();

        internal Stack() { }

        /// <summary>
        /// Add item to the end of the stack.
        /// </summary>
        /// <param name="item">Item to add.</param>
        public void Push(T item)
        {
            Debug.Assert(item != null, "Null item pushed");
            // Remove any instances of item already in the stack.
            Pop(item);
            // Add it to the top.
            _Items.Add(item);
        }

        /// <summary>
        /// Removes all instances of item.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        public void Pop(T item)
        {
            Debug.Assert(item != null, "Null item popped");
            _Items.RemoveAll(candidate => candidate.Equals(item));
        }

        /// <summary>
        /// Returns the object at the top of the Stack without removing it.
        /// </summary>
        /// <returns>Object at the top of the Stack.</returns>
        public T Peek() => _Items[^1];

        /// <summary>
        /// Number of items.
        /// </summary>
        public int Count => _Items.Count;

        internal void Clear()
        {
            _Items.Clear();
        }
    }
}
