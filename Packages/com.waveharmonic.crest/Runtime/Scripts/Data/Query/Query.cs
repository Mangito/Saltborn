// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Potential improvements
// - Half return values
// - Half minGridSize

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Base interface for providers.
    /// </summary>
    public interface IQueryProvider
    {
        // NOTE: Here for documentation reuse.
        /// <param name="hash">Unique ID for calling code. Typically acquired by calling GetHashCode.</param>
        /// <param name="minimumLength">The minimum spatial length of the object, such as the width of a boat. Useful for filtering out detail when not needed. Set to zero to get full available detail.</param>
        /// <param name="points">The world space points that will be queried.</param>
        /// <param name="layer">The layer this query targets.</param>
        /// <returns>The status of the query.</returns>
        internal static int Query(int hash, float minimumLength, Vector3[] points, int layer) => 0;

        /// <summary>
        /// Check if the query results could be retrieved successfully using the return code
        /// from Query method.
        /// </summary>
        /// <param name="status">The query status returned from Query.</param>
        /// <returns>Whether the retrieve was successful.</returns>
        bool RetrieveSucceeded(int status)
        {
            return (status & (int)QueryBase.QueryStatus.RetrieveFailed) == 0;
        }
    }

    interface IQueryable
    {
        int ResultGuidCount { get; }
        int RequestCount { get; }
        int QueryCount { get; }
        void UpdateQueries(WaterRenderer water);
        void CleanUp();
    }

    /// <summary>
    /// Provides heights and other physical data about the water surface. Works by uploading query positions to GPU and computing
    /// the data and then transferring back the results asynchronously. An exception to this is water surface velocities - these can
    /// not be computed on the GPU and are instead computed on the CPU by retaining last frames' query results and computing finite diffs.
    /// </summary>
    abstract class QueryBase : IQueryable
    {
        protected abstract int Kernel { get; }

        // 4 was enough for a long time, but Linux setups seems to demand 7
        const int k_MaximumRequests = 7;
        const int k_MaximumGuids = 1024;

        // We need only two additional queries to compute normals.
        const int k_NormalAdditionalQueryCount = 2;

        readonly WaterRenderer _Water;

        readonly PropertyWrapperCompute _Wrapper;

        readonly System.Action<AsyncGPUReadbackRequest> _DataArrivedAction;

        // Must match value in compute shader
        const int k_ComputeGroupSize = 64;

        static class ShaderIDs
        {
            public static readonly int s_QueryPositions_MinimumGridSizes = Shader.PropertyToID("_Crest_QueryPositions_MinimumGridSizes");
        }


        const float k_FiniteDifferenceDx = 0.1f;

        readonly ComputeBuffer _ComputeBufferQueries;
        readonly ComputeBuffer _ComputeBufferResults;

        internal const int k_DefaultMaximumQueryCount = 4096;

        readonly int _MaximumQueryCount = k_DefaultMaximumQueryCount;
        readonly Vector3[] _QueryPositionXZ_MinimumGridSize = new Vector3[k_DefaultMaximumQueryCount];

        /// <summary>
        /// Holds information about all query points. Maps from unique hash code to position in point array.
        /// </summary>
        sealed class SegmentRegistrar
        {
            // Map from guids to (segment start index, segment end index, frame number when query was made)
            public Dictionary<int, Vector3Int> _Segments = new();
            public int _QueryCount = 0;
        }

        /// <summary>
        /// Since query results return asynchronously and may not return at all (in theory), we keep a ringbuffer
        /// of the registrars of the last frames so that when data does come back it can be interpreted correctly.
        /// </summary>
        sealed class SegmentRegistrarRingBuffer
        {
            // Requests in flight plus 2 held values, plus one current
            static readonly int s_PoolSize = k_MaximumRequests + 2 + 1;

            readonly SegmentRegistrar[] _Segments = new SegmentRegistrar[s_PoolSize];

            public int _SegmentRelease = 0;
            public int _SegmentAcquire = 0;

            public SegmentRegistrar Current => _Segments[_SegmentAcquire];

            public SegmentRegistrarRingBuffer()
            {
                for (var i = 0; i < _Segments.Length; i++)
                {
                    _Segments[i] = new();
                }
            }

            public void AcquireNew()
            {
                var lastIndex = _SegmentAcquire;

                {
                    var newSegmentAcquire = (_SegmentAcquire + 1) % _Segments.Length;

                    if (newSegmentAcquire == _SegmentRelease)
                    {
                        // The last index has incremented and landed on the first index. This shouldn't happen normally, but
                        // can happen if the Scene and Game view are not visible, in which case async readbacks dont get processed
                        // and the pipeline blocks up.
#if !UNITY_EDITOR
                        Debug.LogError("Crest: Query ring buffer exhausted. Please report this to developers.");
#endif
                        return;
                    }

                    _SegmentAcquire = newSegmentAcquire;
                }

                // Copy the registrations across from the previous frame. This makes queries persistent. This is needed because
                // queries are often made from FixedUpdate(), and at high framerates this may not be called, which would mean
                // the query would get lost and this leads to stuttering and other artifacts.
                {
                    _Segments[_SegmentAcquire]._QueryCount = 0;
                    _Segments[_SegmentAcquire]._Segments.Clear();

                    foreach (var segment in _Segments[lastIndex]._Segments)
                    {
                        var age = Time.frameCount - segment.Value.z;

                        // Don't keep queries around if they have not be active in the last 10 frames
                        if (age < 10)
                        {
                            // Compute a new segment range - we may have removed some segments that were too old, so this ensures
                            // we have a nice compact array of queries each frame rather than accumulating persistent air bubbles
                            var newSegment = segment.Value;
                            newSegment.x = _Segments[_SegmentAcquire]._QueryCount;
                            newSegment.y = newSegment.x + (segment.Value.y - segment.Value.x);
                            _Segments[_SegmentAcquire]._QueryCount = newSegment.y + 1;

                            _Segments[_SegmentAcquire]._Segments.Add(segment.Key, newSegment);
                        }
                    }
                }
            }

            public void ReleaseLast()
            {
                _SegmentRelease = (_SegmentRelease + 1) % _Segments.Length;
            }

            public void RemoveRegistrations(int key)
            {
                // Remove the guid for all of the next spare segment registrars. However, don't touch the ones that are being
                // used for active requests.
                var i = _SegmentAcquire;
                while (true)
                {
                    if (_Segments[i]._Segments.ContainsKey(key))
                    {
                        _Segments[i]._Segments.Remove(key);
                    }

                    i = (i + 1) % _Segments.Length;

                    if (i == _SegmentRelease)
                    {
                        break;
                    }
                }
            }

            public void ClearAvailable()
            {
                // Extreme approach - flush all segments for next spare registrars (but don't touch ones being used for active requests)
                var i = _SegmentAcquire;
                while (true)
                {
                    _Segments[i]._Segments.Clear();
                    _Segments[i]._QueryCount = 0;

                    i = (i + 1) % _Segments.Length;

                    if (i == _SegmentRelease)
                    {
                        break;
                    }
                }
            }

            public void ClearAll()
            {
                for (var i = 0; i < _Segments.Length; i++)
                {
                    _Segments[i]._QueryCount = 0;
                    _Segments[i]._Segments.Clear();
                }
            }
        }

        readonly SegmentRegistrarRingBuffer _SegmentRegistrarRingBuffer = new();

        NativeArray<Vector3> _QueryResults;
        float _QueryResultsTime = -1f;
        Dictionary<int, Vector3Int> _ResultSegments;

        NativeArray<Vector3> _QueryResultsLast;
        float _QueryResultsTimeLast = -1f;
        Dictionary<int, Vector3Int> _ResultSegmentsLast;

        struct ReadbackRequest
        {
            public AsyncGPUReadbackRequest _Request;
            public float _DataTimestamp;
            public Dictionary<int, Vector3Int> _Segments;
        }

        readonly List<ReadbackRequest> _Requests = new();

        public enum QueryStatus
        {
            OK = 0,
            RetrieveFailed = 1,
            PostFailed = 2,
            NotEnoughDataForVels = 4,
            VelocityDataInvalidated = 8,
            InvalidDtForVelocity = 16,
        }

        public QueryBase(WaterRenderer water)
        {
            _Water = water;

            _DataArrivedAction = new(DataArrived);

            if (_MaximumQueryCount != water._AnimatedWavesLod.MaximumQueryCount)
            {
                _MaximumQueryCount = water._AnimatedWavesLod.MaximumQueryCount;
                _QueryPositionXZ_MinimumGridSize = new Vector3[_MaximumQueryCount];
            }

            _ComputeBufferQueries = new(_MaximumQueryCount, 12, ComputeBufferType.Default);
            _ComputeBufferResults = new(_MaximumQueryCount, 12, ComputeBufferType.Default);

            _QueryResults = new(_MaximumQueryCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _QueryResultsLast = new(_MaximumQueryCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            var shader = WaterResources.Instance.Compute._Query;
            if (shader == null)
            {
                Debug.LogError($"Crest: Could not load Query compute shader");
                return;
            }
            _Wrapper = new(water.SimulationBuffer, shader, Kernel);
        }

        /// <summary>
        /// Takes a unique request ID and some world space XZ positions, and computes the displacement vector that lands at this position,
        /// to a good approximation. The world space height of the water at that position is then SeaLevel + displacement.y.
        /// </summary>
        protected bool UpdateQueryPoints(int ownerHash, float minSpatialLength, Vector3[] queryPoints, Vector3[] queryNormals)
        {
            if (queryPoints.Length + _SegmentRegistrarRingBuffer.Current._QueryCount > _MaximumQueryCount)
            {
                Debug.LogError($"Crest: Max query count ({_MaximumQueryCount}) exceeded, increase the max query count in the Animated Waves Settings to support a higher number of queries.");
                return false;
            }

            var segmentRetrieved = false;

            // We'll send in 2 points to get normals
            var countPts = queryPoints != null ? queryPoints.Length : 0;
            var countNorms = queryNormals != null ? queryNormals.Length : 0;
            var countTotal = countPts + countNorms * k_NormalAdditionalQueryCount;

            if (_SegmentRegistrarRingBuffer.Current._Segments.TryGetValue(ownerHash, out var segment))
            {
                var segmentSize = segment.y - segment.x + 1;
                if (segmentSize == countTotal)
                {
                    // Update frame count
                    segment.z = Time.frameCount;
                    _SegmentRegistrarRingBuffer.Current._Segments[ownerHash] = segment;

                    segmentRetrieved = true;
                }
                else
                {
                    _SegmentRegistrarRingBuffer.Current._Segments.Remove(ownerHash);
                }
            }

            if (countTotal == 0)
            {
                // No query data
                return false;
            }

            if (!segmentRetrieved)
            {
                if (_SegmentRegistrarRingBuffer.Current._Segments.Count >= k_MaximumGuids)
                {
                    Debug.LogError("Crest: Too many guids registered with CollProviderCompute. Increase s_maxGuids.");
                    return false;
                }

                segment.x = _SegmentRegistrarRingBuffer.Current._QueryCount;
                segment.y = segment.x + countTotal - 1;
                segment.z = Time.frameCount;
                _SegmentRegistrarRingBuffer.Current._Segments.Add(ownerHash, segment);

                _SegmentRegistrarRingBuffer.Current._QueryCount += countTotal;

                //Debug.Log("Crest: Added points for " + guid);
            }

            // The smallest wavelengths should repeat no more than twice across the smaller spatial length. Unless we're
            // in the last LOD - then this is the best we can do.
            var minWavelength = minSpatialLength / 2f;
            var samplesPerWave = 2f;
            var minGridSize = minWavelength / samplesPerWave;

            if (countPts + segment.x > _QueryPositionXZ_MinimumGridSize.Length)
            {
                Debug.LogError("Crest: Too many wave height queries. Increase Max Query Count in the Animated Waves Settings.");
                return false;
            }

            for (var pointi = 0; pointi < countPts; pointi++)
            {
                _QueryPositionXZ_MinimumGridSize[pointi + segment.x].x = queryPoints[pointi].x;
                _QueryPositionXZ_MinimumGridSize[pointi + segment.x].y = queryPoints[pointi].z;
                _QueryPositionXZ_MinimumGridSize[pointi + segment.x].z = minGridSize;
            }

            // To compute each normal, post 2 query points (reuse point above)
            for (var normi = 0; normi < countNorms; normi++)
            {
                var arrIdx = segment.x + countPts + k_NormalAdditionalQueryCount * normi;

                _QueryPositionXZ_MinimumGridSize[arrIdx].x = queryNormals[normi].x + k_FiniteDifferenceDx;
                _QueryPositionXZ_MinimumGridSize[arrIdx].y = queryNormals[normi].z;
                _QueryPositionXZ_MinimumGridSize[arrIdx].z = minGridSize;

                arrIdx += 1;

                _QueryPositionXZ_MinimumGridSize[arrIdx].x = queryNormals[normi].x;
                _QueryPositionXZ_MinimumGridSize[arrIdx].y = queryNormals[normi].z + k_FiniteDifferenceDx;
                _QueryPositionXZ_MinimumGridSize[arrIdx].z = minGridSize;
            }

            return true;
        }

        /// <summary>
        /// Signal that we're no longer servicing queries. Note this leaves an air bubble in the query buffer.
        /// </summary>
        void RemoveQueryPoints(int guid)
        {
            _SegmentRegistrarRingBuffer.RemoveRegistrations(guid);
        }

        /// <summary>
        /// Remove air bubbles from the query array. Currently this lazily just nukes all the registered
        /// query IDs so they'll be recreated next time (generating garbage).
        /// </summary>
        void CompactQueryStorage()
        {
            _SegmentRegistrarRingBuffer.ClearAvailable();
        }

        /// <summary>
        /// Copy out displacements, heights, normals. Pass null if info is not required.
        /// </summary>
        protected bool RetrieveResults(int guid, Vector3[] displacements, float[] heights, Vector3[] normals)
        {
            if (_ResultSegments == null)
            {
                return false;
            }

            // Check if there are results that came back for this guid
            if (!_ResultSegments.TryGetValue(guid, out var segment))
            {
                // Guid not found - no result
                return false;
            }

            var countPoints = 0;
            if (displacements != null) countPoints = displacements.Length;
            if (heights != null) countPoints = heights.Length;
            if (displacements != null && heights != null) Debug.Assert(displacements.Length == heights.Length);
            var countNorms = normals != null ? normals.Length : 0;

            if (countPoints > 0)
            {
                // Retrieve Results
                if (displacements != null) _QueryResults.Slice(segment.x, countPoints).CopyTo(displacements);

                // Retrieve Result heights
                if (heights != null)
                {
                    var seaLevel = _Water.SeaLevel;
                    for (var i = 0; i < countPoints; i++)
                    {
                        heights[i] = seaLevel + _QueryResults[i + segment.x].y;
                    }
                }
            }

            if (countNorms > 0)
            {
                var firstNorm = segment.x + countPoints;

                var dx = -Vector3.right * k_FiniteDifferenceDx;
                var dz = -Vector3.forward * k_FiniteDifferenceDx;
                for (var i = 0; i < countNorms; i++)
                {
                    var p = _QueryResults[i + segment.x];
                    var px = dx + _QueryResults[firstNorm + k_NormalAdditionalQueryCount * i];
                    var pz = dz + _QueryResults[firstNorm + k_NormalAdditionalQueryCount * i + 1];

                    normals[i] = Vector3.Cross(p - px, p - pz).normalized;
                    normals[i].y *= -1f;
                }
            }

            return true;
        }

        /// <summary>
        /// Compute time derivative of the displacements by calculating difference from last query. More complicated than it would seem - results
        /// may not be available in one or both of the results, or the query locations in the array may change.
        /// </summary>
        protected int CalculateVelocities(int ownerHash, Vector3[] results)
        {
            // Need at least 2 returned results to do finite difference
            if (_QueryResultsTime < 0f || _QueryResultsTimeLast < 0f)
            {
                return 1;
            }

            if (!_ResultSegments.TryGetValue(ownerHash, out var segment))
            {
                return (int)QueryStatus.RetrieveFailed;
            }

            if (!_ResultSegmentsLast.TryGetValue(ownerHash, out var segmentLast))
            {
                return (int)QueryStatus.NotEnoughDataForVels;
            }

            if ((segment.y - segment.x) != (segmentLast.y - segmentLast.x))
            {
                // Number of queries changed - can't handle that
                return (int)QueryStatus.VelocityDataInvalidated;
            }

            var dt = _QueryResultsTime - _QueryResultsTimeLast;
            if (dt < 0.0001f)
            {
                return (int)QueryStatus.InvalidDtForVelocity;
            }

            var count = results.Length;
            for (var i = 0; i < count; i++)
            {
                results[i] = (_QueryResults[i + segment.x] - _QueryResultsLast[i + segmentLast.x]) / dt;
            }

            return 0;
        }

        /// <summary>
        /// Per-frame update callback.
        /// </summary>
        /// <param name="water">The current <see cref="WaterRenderer"/>.</param>
        public void UpdateQueries(WaterRenderer water)
        {
#if UNITY_EDITOR
            // Seems to be a terrible memory leak coming from creating async GPU readbacks.
            // This was marked as resolved by Unity and confirmed fixed by forum posts.
            // May be worth keeping. See issue #630 for more details.
            if (!water._HeightQueries && !Application.isPlaying) return;
#endif

            if (_SegmentRegistrarRingBuffer.Current._QueryCount > 0)
            {
                ExecuteQueries();

                // Remove oldest requests if we have hit the limit
                while (_Requests.Count >= k_MaximumRequests)
                {
                    _Requests.RemoveAt(0);
                }

                ReadbackRequest request;
                request._DataTimestamp = Time.time - Time.deltaTime;
                request._Request = AsyncGPUReadback.Request(_ComputeBufferResults, _DataArrivedAction);
                request._Segments = _SegmentRegistrarRingBuffer.Current._Segments;
                _Requests.Add(request);

                _SegmentRegistrarRingBuffer.AcquireNew();
            }
        }

        void ExecuteQueries()
        {
            _ComputeBufferQueries.SetData(_QueryPositionXZ_MinimumGridSize, 0, 0, _SegmentRegistrarRingBuffer.Current._QueryCount);
            _Wrapper.SetBuffer(ShaderIDs.s_QueryPositions_MinimumGridSizes, _ComputeBufferQueries);
            _Wrapper.SetBuffer(Crest.ShaderIDs.s_Target, _ComputeBufferResults);

            var numGroups = (_SegmentRegistrarRingBuffer.Current._QueryCount + k_ComputeGroupSize - 1) / k_ComputeGroupSize;
            _Wrapper.Dispatch(numGroups, 1, 1);
        }

        /// <summary>
        /// Called when a compute buffer has been read back from the GPU to the CPU.
        /// </summary>
        void DataArrived(AsyncGPUReadbackRequest req)
        {
            // Can get callbacks after disable, so detect this.
            if (!_QueryResults.IsCreated)
            {
                _Requests.Clear();
                return;
            }

            // Remove any error requests
            for (var i = _Requests.Count - 1; i >= 0; --i)
            {
                if (_Requests[i]._Request.hasError)
                {
                    _Requests.RemoveAt(i);
                    _SegmentRegistrarRingBuffer.ReleaseLast();
                }
            }

            // Find the last request that was completed
            var lastDoneIndex = _Requests.Count - 1;
            while (lastDoneIndex >= 0 && !_Requests[lastDoneIndex]._Request.done)
            {
                --lastDoneIndex;
            }

            // If there is a completed request, process it
            if (lastDoneIndex >= 0)
            {
                // Update "last" results
                (_QueryResults, _QueryResultsLast) = (_QueryResultsLast, _QueryResults);
                _QueryResultsTimeLast = _QueryResultsTime;
                _ResultSegmentsLast = _ResultSegments;

                var data = _Requests[lastDoneIndex]._Request.GetData<Vector3>();
                data.CopyTo(_QueryResults);
                _QueryResultsTime = _Requests[lastDoneIndex]._DataTimestamp;
                _ResultSegments = _Requests[lastDoneIndex]._Segments;
            }

            // Remove all the requests up to the last completed one
            for (var i = lastDoneIndex; i >= 0; --i)
            {
                _Requests.RemoveAt(i);
                _SegmentRegistrarRingBuffer.ReleaseLast();
            }
        }

        /// <summary>
        /// On destroy, to clean up resources.
        /// </summary>
        public void CleanUp()
        {
            _ComputeBufferQueries.Dispose();
            _ComputeBufferResults.Dispose();

            if (_QueryResults.IsCreated) _QueryResults.Dispose();
            if (_QueryResultsLast.IsCreated) _QueryResultsLast.Dispose();

            _SegmentRegistrarRingBuffer.ClearAll();
        }

        public virtual int Query(int ownerHash, float minSpatialLength, Vector3[] queryPoints, Vector3[] results)
        {
            var result = (int)QueryStatus.OK;

            if (!UpdateQueryPoints(ownerHash, minSpatialLength, queryPoints, null))
            {
                result |= (int)QueryStatus.PostFailed;
            }

            if (!RetrieveResults(ownerHash, results, null, null))
            {
                result |= (int)QueryStatus.RetrieveFailed;
            }

            return result;
        }

        public int ResultGuidCount => _ResultSegments != null ? _ResultSegments.Count : 0;

        public int RequestCount => _Requests != null ? _Requests.Count : 0;

        public int QueryCount => _SegmentRegistrarRingBuffer != null ? _SegmentRegistrarRingBuffer.Current._QueryCount : 0;
    }

    static partial class Extensions
    {
        public static void UpdateQueries(this IQueryProvider self, WaterRenderer water) => (self as IQueryable)?.UpdateQueries(water);
        public static void CleanUp(this IQueryProvider self) => (self as IQueryable)?.CleanUp();
    }
}
