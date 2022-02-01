// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BitmapVectorizer
{
    internal sealed class Path : IEnumerable<Path>, IPathList
    {
        #region Fields

        internal readonly int area;
        internal IntPoint[] points;
        internal readonly bool sign;
        internal int[]? lon;
        internal SumStruct[]? sums;
        internal int[]? po;
        internal Path? Next;
        internal Path? Sibling;
        internal Path? ChildList;

        #endregion

        #region Properties

        /// <summary>
        /// pt[len]: path as extracted from bitmap
        /// </summary>
        public IReadOnlyList<IntPoint> Points => points;

        /// <summary>
        /// lon[len]: (i,lon[i]) = longest straight line from i
        /// </summary>
        public IReadOnlyList<int>? Lon => lon;

        /// <summary>
        /// sums[len + 1]: cache for fast summing
        /// </summary>
        public IReadOnlyList<SumStruct>? Sums => sums;

        /// <summary>
        /// po[m]: optimal polygon
        /// </summary>
        public IReadOnlyList<int>? Po => po;

        /// <summary>
        /// curve[m]: array of curve elements
        /// </summary>
        internal PrivCurve? Curves { get; set; }

        /// <summary>
        /// ocurve[om]: array of curve elements
        /// </summary>
        internal PrivCurve? OptimizedCurves { get; set; }

        /// <summary>
        /// final curve: this points to either curve or ocurve.
        /// </summary>
        public PrivCurve? FCurves => OptimizedCurves ?? Curves;

        #endregion

        #region Properties

        internal IntPoint Point0
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => points[0];
        }

        #endregion

        #region Constructor

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Path(long area, bool sign, IntPoint[] points)
        {
            this.area = area <= int.MaxValue ? (int)area : int.MaxValue; /* avoid overflow */
            this.sign = sign;
            this.points = points;
        }

        #endregion

        #region Public Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InsertBeforeHook(Hook hook)
        {
            Next = hook.Value;
            hook.Value = this;
            hook.Set(() => Next, p => Next = p);
        }

        public Interval SetLimits(VECTOR dir)
        {
            if (FCurves is null) { throw new InvalidOperationException(); }
            FLOAT prod = VectorHelper.Dot(FCurves.GetEndPoint(0), dir);
            Interval limits = new Interval(prod);
            ForEach(p => p.FCurves!.SetLimits(limits, dir));
            return limits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEach(Action<Path> action) => ForEach(action, NextSelector);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachSibling(Action<Path> action) => ForEach(action, SiblingSelector);

        #endregion

        #region Private Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForEach(Action<Path> action, Func<Path, Path?> selector)
        {
            for (Path? p = this; p != null; p = selector(p))
            {
                action(p);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Path? NextSelector(Path p) => p.Next;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Path? SiblingSelector(Path p) => p.Sibling;

        #endregion

        #region IEnumerable

        public IEnumerator<Path> GetEnumerator()
        {
            for (Path? p = this; p != null; p = NextSelector(p))
            {
                yield return p;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
