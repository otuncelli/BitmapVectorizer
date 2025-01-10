// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BitmapVectorizer;

internal sealed class PrivCurve : IReadOnlyList<Segment>
{
    private readonly Segment[] segments;
    private readonly VECTOR[] vertices;
    private readonly FLOAT[] alphas;
    private readonly FLOAT[] alpha0s;
    private readonly FLOAT[] betas;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PrivCurve(int m)
    {
        segments = new Segment[m];
        vertices = new VECTOR[m];
        alphas = new FLOAT[m];
        alpha0s = new FLOAT[m];
        betas = new FLOAT[m];
    }

    public VECTOR Point0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetEndPoint(segments.Length - 1);
    }

    public Segment this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => segments[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal set => segments[index] = value;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => segments.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VECTOR GetVertex(int index) => vertices[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetVertex(int index, in VECTOR p) => vertices[index] = p;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FLOAT GetAlpha(int index) => alphas[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetAlpha(int index, in FLOAT value) => alphas[index] = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FLOAT GetAlpha0(int index) => alpha0s[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetAlpha0(int index, in FLOAT value) => alpha0s[index] = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetBeta(int index) => betas[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetBeta(int index, in FLOAT value) => betas[index] = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VECTOR GetEndPoint(int index) => segments[index].EndPoint;

    internal void SetLimits(Interval interval, in VECTOR dir)
    {
        if (segments.Length == 0) { return; }
        segments[0].SetLimits(interval, Point0, dir);
        for (int k = 1; k < Count; k++)
        {
            segments[k].SetLimits(interval, GetEndPoint(k - 1), dir);
        }
    }

    public VECTOR[] Tessellate(int res = 30)
    {
        Ensure.IsGreaterThan(res, 0, nameof(res));

        VECTOR start = Point0;
        int capacity = segments.Sum(s => s.Type == SegmentType.Corner ? 2 : res + 1);
        VECTOR[] points = new VECTOR[capacity];
        int i = 0;
        foreach (Segment segment in segments)
        {
            VECTOR[] pts = segment.Tessellate(start, res);
            Array.Copy(pts, 0, points, i, pts.Length);
            i += pts.Length;
            start = segment.EndPoint;
        }
        return points;
    }

    #region IEnumerable

    public IEnumerator<Segment> GetEnumerator()
    {
        return segments.AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return segments.GetEnumerator();
    }

    #endregion
}
