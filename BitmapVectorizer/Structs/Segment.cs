// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BitmapVectorizer;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct Segment
{
    public readonly SegmentType Type;

    public readonly VECTOR C0;

    public readonly VECTOR C1;

    public readonly VECTOR EndPoint;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Segment(in VECTOR c0, in VECTOR c1, in VECTOR endpoint)
        : this(c0, c1, endpoint, SegmentType.Bezier)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Segment(in VECTOR c1, in VECTOR endpoint)
        : this(default, c1, endpoint, SegmentType.Corner)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Segment(in VECTOR c1, in VECTOR c2, in VECTOR endpoint, SegmentType type)
    {
        C0 = c1;
        C1 = c2;
        EndPoint = endpoint;
        Type = type;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VECTOR[] Tessellate(in VECTOR start, int res = 30)
    {
        if (Type == SegmentType.Corner)
        {
            return [C1, EndPoint];
        }

        FLOAT t = 1 / (FLOAT)res;
        FLOAT tt = t * t;
        VECTOR f = start;
        VECTOR fd = 3 * (C0 - start) * t;
        VECTOR fdd_per_2 = 3 * (start - 2 * C0 + C1) * tt;
        VECTOR fddd_per_2 = 3 * (3 * (C0 - C1) + EndPoint - start) * tt * t;
        VECTOR fddd = 2 * fddd_per_2;
        VECTOR fdd = 2 * fdd_per_2;
        VECTOR fddd_per_6 = fddd_per_2 / 3;

        VECTOR[] points = new VECTOR[res + 1];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = f;
            f += fd + fdd_per_2 + fddd_per_6;
            fd += fdd + fddd_per_2;
            fdd += fddd;
            fdd_per_2 += fddd_per_2;
        }
        return points;
    }

    public void SetLimits(Interval interval, in VECTOR a, in VECTOR dir)
    {
        if (Type == SegmentType.Bezier)
        {
            FLOAT x0 = VectorHelper.Dot(a, dir);
            FLOAT x1 = VectorHelper.Dot(C0, dir);
            FLOAT x2 = VectorHelper.Dot(C1, dir);
            FLOAT x3 = VectorHelper.Dot(EndPoint, dir);
            BezierLimit(interval, x0, x1, x2, x3);
        }
        else
        {
            FLOAT x0 = VectorHelper.Dot(C1, dir);
            FLOAT x1 = VectorHelper.Dot(EndPoint, dir);
            interval.Extend(x0);
            interval.Extend(x1);
        }
    }

    public void CopyTo(VECTOR[] array, int index)
    {
        if (Type == SegmentType.Bezier)
        {
            array.SetValue(C0, index);
            array.SetValue(C1, index + 1);
            array.SetValue(EndPoint, index + 2);
        }
        else
        {
            array.SetValue(C1, index);
            array.SetValue(EndPoint, index + 1);
        }
    }

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FLOAT Bezier(FLOAT t, FLOAT x0, FLOAT x1, FLOAT x2, FLOAT x3)
    {
        FLOAT ti = 1 - t;
        FLOAT t0 = ti * ti * ti;
        FLOAT t1 = 3 * ti * ti * t;
        FLOAT t2 = 3 * ti * t * t;
        FLOAT t3 = t * t * t;
        return (t0 * x0) + (t1 * x1) + (t2 * x2) + (t3 * x3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BezierLimit(Interval interval, FLOAT x0, FLOAT x1, FLOAT x2, FLOAT x3)
    {
        /* the min and max of a cubic curve segment are attained at one of
           at most 4 critical points: the 2 endpoints and at most 2 local
           extrema. We don't check the first endpoint, because all our
           curves are cyclic so it's more efficient not to check endpoints
           twice. */

        /* endpoint */
        interval.Extend(x3);

        if (interval.IsIn(x1) && interval.IsIn(x2))
        {
            return;
        }

        FLOAT a = -3 * x0 + 9 * x1 - 9 * x2 + 3 * x3;
        FLOAT b = 6 * x0 - 12 * x1 + 6 * x2;
        FLOAT c = -3 * x0 + 3 * x1;
        FLOAT d = b * b - 4 * a * c;

        if (d > 0)
        {
            FLOAT x;
            FLOAT r = MathHelper.Sqrt(d);
            FLOAT t = (-b - r) / (a * 2);
            if (t > 0 && t < 1)
            {
                x = Bezier(t, x0, x1, x2, x3);
                interval.Extend(x);
            }
            t = (-b + r) / (a * 2);
            if (t > 0 && t < 1)
            {
                x = Bezier(t, x0, x1, x2, x3);
                interval.Extend(x);
            }
        }
    }

    #endregion
}
