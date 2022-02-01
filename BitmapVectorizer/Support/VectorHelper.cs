// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
#if NUMERICS
using System.Numerics;
#endif
using System.Runtime.CompilerServices;

namespace BitmapVectorizer
{
    internal static class VectorHelper
    {
        /// <summary>
        /// 2D vector cross product analog.<br />
        /// The cross product of 2D vectors results in a 3D vector with only a z component.<br />
        /// This function returns the magnitude of the z value.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Cross(in VECTOR left, in VECTOR right)
        {
            // System.Numerics.Vector2 doesn't have a method for this
            // Alternatives:
            // Vector3 v0 = new Vector3(left, 0);
            // Vector3 v1 = new Vector3(right, 0);
            // return Vector3.Cross(v0, v1).Z;
            // or
            // return Vector2.Dot(left, new Vector2(right.Y, -right.X));
#if WINDOWS && !NUMERICS
            return VECTOR.CrossProduct(left, right);
#else
            return left.X * right.Y - left.Y * right.X;
#endif
        }

        /// <summary>
        /// 2D vector cross product analog.<br />
        /// The cross product of 2D vectors results in a 3D vector with only a z component.<br />
        /// This function returns the magnitude of the z value.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Cross(in IntPoint left, in IntPoint right)
        {
            return left.X * right.Y - left.Y * right.X;
        }

        /// <summary>
        /// Calculates cross product of three vectors.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>the area of the parallelogram</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Cross(in VECTOR p0, in VECTOR p1, in VECTOR p2)
        {
            return Cross(p1 - p0, p2 - p0);
        }

        /// <summary>
        /// Calculates cross product of four vectors.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Cross(in VECTOR p0, in VECTOR p1, in VECTOR p2, in VECTOR p3)
        {
            return Cross(p1 - p0, p3 - p2);
        }

        /// <summary>
        /// Calculates dot product of two vectors.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Dot(in VECTOR left, in VECTOR right)
        {
#if NUMERICS
            return VECTOR.Dot(left, right);
#else
            return left.X * right.X + left.Y * right.Y;
#endif
        }

        /// <summary>
        /// Calculates dot product of three vectors.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Dot(in VECTOR p0, in VECTOR p1, in VECTOR p2)
        {
            return Dot(p1 - p0, p2 - p0);
        }

        /// <summary>
        /// Calculates dot product of four vectors.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Dot(in VECTOR p0, in VECTOR p1, in VECTOR p2, in VECTOR p3)
        {
            return Dot(p1 - p0, p3 - p2);
        }

        /// <summary>
        /// Calculates distance between two points.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Distance(in VECTOR left, in VECTOR right)
        {
#if NUMERICS
            return VECTOR.Distance(left, right);
#else
            VECTOR diff = left - right;
            return MathHelper.Sqrt(Dot(diff, diff));
#endif
        }

        /// <summary>
        /// Calculates a point of a bezier curve.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VECTOR Bezier(FLOAT t, in VECTOR p0, in VECTOR p1, in VECTOR p2, in VECTOR p3)
        {
            FLOAT ti = 1 - t;
            FLOAT t0 = ti * ti * ti;
            FLOAT t1 = 3 * ti * ti * t;
            FLOAT t2 = 3 * ti * t * t;
            FLOAT t3 = t * t * t;
            return (t0 * p0) + (t1 * p1) + (t2 * p2) + (t3 * p3);
        }

        /// <summary>
        /// Calculates the point t in [0..1] on the (convex) bezier curve
        /// (p0,p1,p2,p3) which is tangent to q1-q0. Return -1.0 if there is no
        /// solution in [0..1]. 
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="q0"></param>
        /// <param name="q1"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Tangent(in VECTOR p0, in VECTOR p1, in VECTOR p2, in VECTOR p3, in VECTOR q0, in VECTOR q1)
        {
            /* (1-t)^2 A + 2(1-t)t B + t^2 C = 0 */
            FLOAT A = Cross(p0, p1, q0, q1);
            FLOAT B = Cross(p1, p2, q0, q1);
            FLOAT C = Cross(p2, p3, q0, q1);

            /* a t^2 + b t + c = 0 */
            FLOAT a = A - 2 * B + C;
            FLOAT b = -2 * A + 2 * B;
            FLOAT c = A;

            FLOAT d = b * b - 4 * a * c;

            if (a == 0 || d < 0)
            {
                return -1;
            }

            FLOAT s = MathHelper.Sqrt(d);

            FLOAT r1 = (-b + s) / (2 * a);
            FLOAT r2 = (-b - s) / (2 * a);

            if (r1 >= 0 && r1 <= 1)
            {
                return r1;
            }
            else if (r2 >= 0 && r2 <= 1)
            {
                return r2;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Range over the straight line segment [a,b] when lambda ranges over [0,1].
        /// </summary>
        /// <param name="lambda">Scale.</param>
        /// <param name="a">Start point.</param>
        /// <param name="b">Stop point.</param>
        /// <returns>Point on the segment.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VECTOR Interval(FLOAT lambda, in VECTOR a, in VECTOR b)
        {
#if NUMERICS
            return VECTOR.Lerp(a, b, lambda);
#else
            return (b - a) * lambda + a;
#endif
        }

        /// <summary>
        /// return a direction that is 90 degrees counterclockwise from p2-p0,
        /// but then restricted to one of the major wind directions (n, nw, w, etc).
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VECTOR DorthInfty(in VECTOR p0, in VECTOR p2)
        {
            VECTOR p = p2 - p0;
            FLOAT x = -Math.Sign(p.Y);
            FLOAT y = Math.Sign(p.X);
            return new VECTOR(x, y);
        }

        /// <summary>
        /// ddenom/dpara have the property that the square of radius 1 centered
        /// at p1 intersects the line p0p2 iff |dpara(p0, p1, p2)| <= ddenom(p0, p2)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FLOAT Ddenom(in VECTOR p0, in VECTOR p2)
        {
            return Cross(p2 - p0, DorthInfty(p0, p2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VECTOR Abs(in VECTOR v)
        {
#if NUMERICS
            return VECTOR.Abs(v);
#else
            return new VECTOR(Math.Abs(v.X), Math.Abs(v.Y));
#endif
        }
    }
}
