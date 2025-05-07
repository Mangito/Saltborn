// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections;
using System.Collections.Generic;

namespace WaveHarmonic.Crest.Utility
{
    /// <summary>
    /// This is a list this is meant to be similar in behaviour to the C#
    /// SortedList, but without allocations when used directly in a foreach loop.
    ///
    /// It works by using a regular list as as backing and ensuring that it is
    /// sorted when the enumerator is accessed and used. This is a simple approach
    /// that means we avoid sorting each time an element is added, and helps us
    /// avoid having to develop our own more complex data structure.
    /// </summary>
    sealed class SortedList<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable
    {
        public int Count => _BackingList.Count;

        readonly List<KeyValuePair<TKey, TValue>> _BackingList = new();
        readonly System.Comparison<TKey> _Comparison;
        bool _NeedsSorting = false;

        int Comparison(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
        {
            return _Comparison(x.Key, y.Key);
        }

        public SortedList(System.Comparison<TKey> comparison)
        {
            // We provide the only constructors that SortedList provides that
            // we need. We wrap the input IComparer to ensure that our backing list
            // is sorted in the same way a SortedList would be with the same one.
            _Comparison = comparison;
        }

        public void Add(TKey key, TValue value)
        {
            _BackingList.Add(new(key, value));
            _NeedsSorting = true;
        }

        public bool Remove(TValue value)
        {
            // This remove function has a fairly high complexity, as we need to search
            // the list for a matching Key-Value pair, and then remove it. However,
            // for the small lists we work with this is fine, as we don't use this
            // function more often. But it's worth bearing in mind if we decide to
            // expand where we use this list. At that point we might need to take a
            // different approach.

            var removeIndex = -1;
            var index = 0;
            foreach (var item in _BackingList)
            {
                if (item.Value.Equals(value))
                {
                    removeIndex = index;
                }

                index++;
            }

            if (removeIndex > -1)
            {
                // Remove method produces garbage.
                _BackingList.RemoveAt(removeIndex);
            }

            return removeIndex > -1;
        }

        public void Clear()
        {
            _BackingList.Clear();
            _NeedsSorting = false;
        }

        #region GetEnumerator
        public List<KeyValuePair<TKey, TValue>>.Enumerator GetEnumerator()
        {
            ResortArrays();
            return _BackingList.GetEnumerator();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        void ResortArrays()
        {
            if (_NeedsSorting)
            {
                // @GC: Allocates 112B.
                _BackingList.Sort(Comparison);
            }
            _NeedsSorting = false;
        }
    }
}
