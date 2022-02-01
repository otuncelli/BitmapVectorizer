// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BitmapVectorizer
{
    /// <summary>
    /// Holds the components of a Point.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{X={X},Y={Y}}")]
    internal readonly struct IntPoint : IEquatable<IntPoint>
    {
        public static readonly IntPoint Zero = default;

        /// <summary>
        /// x-component
        /// </summary>
        public readonly int X;

        /// <summary>
        /// x-component
        /// </summary>
        public readonly int Y;

        /// <summary>
        /// Creates a point/vector
        /// </summary>
        /// <param name="x">x-component</param>
        /// <param name="y">y-component</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Deconstructs this point/vector into two integers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out int x, out int y)
        {
            x = X;
            y = Y;
        }

        #region IEquatable

        public override string ToString()
        {
            return $"{{X={X},Y={Y}}}";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IntPoint other)
        {
            return X == other.X && Y == other.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            return (obj is IntPoint p) && Equals(p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in IntPoint left, in IntPoint right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in IntPoint left, in IntPoint right)
        {
            return !(left == right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        #endregion

        #region Operator overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPoint operator -(in IntPoint left, in IntPoint right)
        {
            return new IntPoint(left.X - right.X, left.Y - right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPoint operator +(in IntPoint left, in IntPoint right)
        {
            return new IntPoint(left.X + right.X, left.Y + right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator VECTOR(in IntPoint p)
        {
            return new VECTOR(p.X, p.Y);
        }

        #endregion
    }
}
