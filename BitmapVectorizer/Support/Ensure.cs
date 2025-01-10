// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Collections;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace BitmapVectorizer;

internal static class Ensure
{
#if NET6_0_OR_GREATER
    [return: NotNullIfNotNull(nameof(param))]
#endif
    public static T IsNotNull<T>(T param, string paramName)
    {
        return param == null
            ? throw new ArgumentNullException(paramName)
            : param;
    }

    public static IntPtr IsNotNull(IntPtr param, string paramName)
    {
        return param == IntPtr.Zero
            ? throw new ArgumentNullException(paramName)
            : param;
    }

    public static double IsNotNaN(double param, string paramName)
    {
        return double.IsNaN(param) 
            ? throw new ArgumentException("Value can not be NaN.", paramName)
            : param;
    }

    public static T IsNotNullOrEmpty<T>(T param, string paramName) 
        where T : ICollection
    {
        param = IsNotNull(param, paramName);
        return param.Count == 0
            ? throw new ArgumentException("Collection can not be empty.", paramName)
            : param;
    }

    public static T IsInRange<T>(T param, T min, T max, string paramName) 
        where T : notnull, IComparable<T>
    {
        return param.CompareTo(min) >= 0 && param.CompareTo(max) <= 0
            ? param
            : throw new ArgumentOutOfRangeException(paramName, $"Value must be between {min} and {max}.");
    }

    public static T IsGreaterThan<T>(T param, T comparand, string paramName) 
        where T : notnull, IComparable<T>
    {
        return param.CompareTo(comparand) > 0
            ? param
            : throw new ArgumentOutOfRangeException(paramName, $"Value must be greater than {comparand}.");
    }

    public static T IsGreaterThanOrEqualTo<T>(T param, T comparand, string paramName) 
        where T : notnull, IComparable<T>
    {
        return param.CompareTo(comparand) >= 0
            ? param
            : throw new ArgumentOutOfRangeException(paramName, $"Value must be greater than or equal to {comparand}.");
    }

    public static T IsLessThan<T>(T param, T comparand, string paramName) 
        where T : notnull, IComparable<T>
    {
        return param.CompareTo(comparand) < 0
            ? param
            : throw new ArgumentOutOfRangeException(paramName, $"Value must be less than {comparand}.");
    }

    public static T IsLessThanOrEqualTo<T>(T param, T comparand, string paramName) 
        where T : notnull, IComparable<T>
    {
        return param.CompareTo(comparand) <= 0
            ? param
            : throw new ArgumentOutOfRangeException(paramName, $"Value must be less than or equal to {comparand}.");
    }

    public static T IsEqualTo<T>(T param, T comparand, string paramName) 
        where T: notnull
    {
        return param.Equals(comparand)
            ? param
            : throw new ArgumentException($"Value must be equal to {comparand}.", paramName);
    }
}
