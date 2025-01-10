// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BitmapVectorizer;

[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("{X={X},Y={Y},X2={X2},XY={XY},Y2={Y2}}")]
internal readonly struct SumStruct
{
    public readonly int X;
    public readonly int Y;
    public readonly int XY;
    public readonly int X2;
    public readonly int Y2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SumStruct(int x, int y, int x2, int xy, int y2)
    {
        X = x;
        Y = y;
        X2 = x2;
        XY = xy;
        Y2 = y2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out FLOAT x, out FLOAT y, out FLOAT x2, out FLOAT xy, out FLOAT y2)
    {
        x = X;
        y = Y;
        x2 = X2;
        xy = XY;
        y2 = Y2;
    }

    public override string ToString()
    {
        return $"{{X={X},Y={Y},X2={X2},XY={XY},Y2={Y2}}}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SumStruct operator +(in SumStruct s, in IntPoint p)
    {
        return new SumStruct(s.X + p.X, s.Y + p.Y, s.X2 + p.X * p.X, s.XY + p.X * p.Y, s.Y2 + p.Y * p.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SumStruct operator +(in SumStruct s1, in SumStruct s2)
    {
        return new SumStruct(s1.X + s2.X, s1.Y + s2.Y, s1.X2 + s2.X2, s1.XY + s2.XY, s1.Y2 + s2.Y2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SumStruct operator -(in SumStruct s1, in SumStruct s2)
    {
        return new SumStruct(s1.X - s2.X, s1.Y - s2.Y, s1.X2 - s2.X2, s1.XY - s2.XY, s1.Y2 - s2.Y2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SumStruct operator *(int r, in SumStruct s)
    {
        return new SumStruct(s.X * r, s.Y * r, s.X2 * r, s.XY * r, s.Y2 * r);
    }
}
