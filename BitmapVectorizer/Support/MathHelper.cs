// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Runtime.CompilerServices;

namespace BitmapVectorizer;

internal static class MathHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Cyclic(int a, int b, int c)
    {
        /* return 1 if a <= b < c < a, in a cyclic sense (mod n) */
        return a <= c ? a <= b && b < c : a <= b || b < c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mod(int a, int n)
    {
        /* Note: the "mod" macro works correctly for
           negative a. Also note that the test for a>=n, while redundant,
           speeds up the mod function by 70% in the average case (significant
           since the program spends about 16% of its time here - or 40%
           without the test). */
        return a >= n ? a % n : a >= 0 ? a : n - 1 - (-1 - a) % n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorDiv(int a, int n)
    {
        /* The "floordiv" macro returns the largest integer
           <= a/n, and again this works correctly for negative a, as long as
           a,n are integers and n>0. */
        return a >= 0 ? a / n : -1 - (-1 - a) / n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FLOAT Clamp(FLOAT value, FLOAT min, FLOAT max)
    {
        return value < min ? min : value > max ? max : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FLOAT Sqrt(FLOAT d)
    {
#if WINDOWS && NET6_0_OR_GREATER
        return Math.Sqrt(d);
#elif NET6_0_OR_GREATER
        return MathF.Sqrt(d);
#else
        return (FLOAT)Math.Sqrt(d);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (FLOAT Sin, FLOAT Cos) SinCos(FLOAT d)
    {
#if WINDOWS && NET6_0_OR_GREATER
        return Math.SinCos(d);
#elif NET6_0_OR_GREATER
        return MathF.SinCos(d);
#else
        return ((FLOAT)Math.Sin(d), (FLOAT)Math.Cos(d));
#endif
    }
}
