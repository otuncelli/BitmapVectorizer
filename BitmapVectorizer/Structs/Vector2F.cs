// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

#if !NUMERICS && !WINDOWS
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BitmapVectorizer
{
    /// <summary>
    /// Holds the components of a Point/Vector
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{X={X},Y={Y}}")]
    internal readonly struct Vector2F : IEquatable<Vector2F>
    {
        public static readonly Vector2F Zero = default;

        /// <summary>
        /// x-component
        /// </summary>
        public readonly FLOAT X;

        /// <summary>
        /// y-component
        /// </summary>
        public readonly FLOAT Y;

        /// <summary>
        /// Creates a point/vector
        /// </summary>
        /// <param name="x">x-component</param>
        /// <param name="y">y-component</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2F(FLOAT x, FLOAT y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Deconstructs this point/vector into two doubles.
        /// </summary>
        /// <param name="x">The out value for X.</param>
        /// <param name="y">The out value for Y.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out FLOAT x, out FLOAT y)
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
        public bool Equals(Vector2F other)
        {
            return X == other.X && Y == other.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            return (obj is Vector2F p) && Equals(p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return HashCode.Combine(X.GetHashCode(), Y.GetHashCode());
        }

        #endregion

        #region Operator overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2F operator -(in Vector2F left, in Vector2F right)
        {
            return new Vector2F(left.X - right.X, left.Y - right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2F operator +(in Vector2F left, in Vector2F right)
        {
            return new Vector2F(left.X + right.X, left.Y + right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2F operator *(in Vector2F p, FLOAT scalar)
        {
            return new Vector2F(p.X * scalar, p.Y * scalar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2F operator *(FLOAT scalar, in Vector2F p)
        {
            return p * scalar;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2F operator /(in Vector2F p, FLOAT scalar)
        {
            return p * (1 / scalar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in Vector2F left, in Vector2F right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in Vector2F left, in Vector2F right)
        {
            return !(left == right);
        }

        #endregion
    }
}
#endif
