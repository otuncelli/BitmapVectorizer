// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

#if NUMERICS
global using VECTOR = System.Numerics.Vector2;
global using FLOAT = System.Single;
#elif WINDOWS
global using VECTOR = System.Windows.Vector;
global using FLOAT = System.Double;
#else
global using VECTOR = BitmapVectorizer.Vector2F;
global using FLOAT = System.Double;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace BitmapVectorizer.Tests
{
    [TestClass]
    public class PotraceTests
    {
        [TestMethod]
        public void NoPathTest()
        {
            using PotraceBitmap bm = new(1, 1);
            TraceResult trace = bm.Trace();
            Debug.Assert(trace == null);
        }

        [TestMethod]
        public void TesselateTest()
        {
#if !NUMERICS
            FLOAT[,] regular = new FLOAT[,]
            {
                {13.31417446,23.4900536},{11.65190728,22.51888426},{10.37429236,21.22192386},{9.47895097,19.68359985},
                {8.96350443,17.98833963},{8.82557401,16.22057063},{9.062781,14.46472026},{9.67274671,12.80521596},
                {10.65309241,11.32648514},{12.00143941,10.11295521},{13.71540898,9.24905362},{13.71540898,9.24905362},
                {15.68274205,8.80074698},{17.55445232,8.81676355},{19.2839093,9.24731412},{20.82448247,10.04260947},
                {22.12954135,11.15286036},{23.15245542,12.5282776},{23.84659418,14.11907195},{24.16532713,15.87545421},
                {24.06202378,17.74763514},{23.4900536,19.68582554},{23.4900536,19.68582554},{22.91315459,20.80514675},
                {22.19509034,21.77643139},{21.35207374,22.59361829},{20.40031768,23.25064632},{19.35603504,23.74145431},
                {18.23543873,24.05998113},{17.05474163,24.20016562},{15.83015662,24.15594663},{14.5778966,23.921263},
                {13.31417446,23.4900536}
            };
#else
            FLOAT[,] regular = new FLOAT[,]
            {
                {13.31418f,23.49006f},{12.19485f,22.91316f},{11.22357f,22.19509f},{10.40638f,21.35207f},{9.749356f,20.40032f},
                {9.258549f,19.35604f},{8.940022f,18.23544f},{8.799837f,17.05474f},{8.844055f,15.83016f},{9.078738f,14.5779f},
                {9.509947f,13.31418f},{9.509947f,13.31418f},{10.48112f,11.65191f},{11.77808f,10.37429f},{13.3164f,9.478953f},
                {15.01166f,8.963508f},{16.77943f,8.825578f},{18.53528f,9.062785f},{20.19479f,9.672751f},{21.67352f,10.6531f},
                {22.88705f,12.00145f},{23.75095f,13.71542f},{23.75095f,13.71541f},{24.19925f,15.68274f},{24.18324f,17.55445f},
                {23.75268f,19.28391f},{22.95739f,20.82449f},{21.84714f,22.12955f},{20.47172f,23.15246f},{18.88093f,23.8466f},
                {17.12454f,24.16533f},{15.25236f,24.06203f},{13.31417f,23.49006f}
            };
#endif

            TraceResult trace;
            using (var bm = GetCircleBitmap())
            {
                trace = bm.Trace();
            }
            VECTOR[] tesselate = trace.FirstPath.FCurves.Tessellate(res: 10);
            Debug.Assert(tesselate.Length == regular.GetLength(0));
            for (int i = 0; i < regular.GetLength(0); i++)
            {
                for (int j = 0; j < regular.GetLength(1); j++)
                {
                    FLOAT comparand = regular[i, j];
                    FLOAT d = j == 0 ? tesselate[i].X : tesselate[i].Y;
                    d = Math.Abs(d - comparand);
                    Debug.Assert(d < 1e-5f);
                }
            }
        }

        [TestMethod]
        public void TestTrace()
        {
            double[,] expected = new double[,]
            {
                {8.0,8.0},{16.0,8.0},{24.0,8.0},{24.0,16.0},
                {24.0,24.0},{16.0,24.0},{8.0,24.0},{8.0,16.0}
            };

            TraceResult trace;
            using (PotraceBitmap bm = GetRectangleBitmap())
            {
                trace = bm.Trace();
            }
            int y = 0;
            trace.FirstPath.ForEachSibling(p =>
            {
                foreach (Segment curve in p.FCurves)
                {
                    if (curve.Type == SegmentType.Corner)
                    {
                        Debug.Assert(expected[y, 0] == curve.C1.X);
                        Debug.Assert(expected[y, 1] == curve.C1.Y);
                        Debug.Assert(expected[y + 1, 0] == curve.EndPoint.X);
                        Debug.Assert(expected[y + 1, 1] == curve.EndPoint.Y);
                        y += 2;
                    }
                    else
                    {
                        throw new AssertFailedException();
                    }
                }
            });
        }

        [TestMethod]
        public void TreeTest()
        {
            TraceResult trace;
            using(var bm = GetFrameBitmap())
            {
                trace = bm.Trace();
            }
            Debug.Assert(trace.FirstPath.Next != null);
            Debug.Assert(trace.FirstPath.ChildList != null);
        }

        private static PotraceBitmap GetFrameBitmap()
        {
            PotraceBitmap bm = new(32, 32);
            // Make a frame
            const int margin = 8;
            const int framewidth = 2;
            for (int y = 0; y < bm.Height; y++)
            {
                for (var x = 0; x < bm.Width; x++)
                {
                    if (x >= margin && y >= margin &&
                        y < bm.Height - margin && x < bm.Width - margin &&
                        (x < margin + framewidth || y < margin + framewidth ||
                         y >= bm.Height - margin - framewidth || 
                         x >= bm.Width - margin - framewidth))
                    {
                        bm[x, y] = true;
                    }
                }
            }
            return bm;
        }

        private static PotraceBitmap GetRectangleBitmap()
        {
            PotraceBitmap bm = new(32, 32);
            // Make a rectangle
            const int margin = 8;
            for (int y = 0; y < bm.Height; y++)
            {
                for (var x = 0; x < bm.Width; x++)
                {
                    bm[x, y] = x >= margin && y >= margin && y < bm.Height - margin && x < bm.Width - margin;
                }
            }
            return bm;
        }

        private static PotraceBitmap GetCircleBitmap()
        {
            PotraceBitmap bm = new(32, 32);
            // Make a circle
            const int radius2 = 8 * 8;
            for (int j = 0; j < bm.Height; j++)
            {
                int y = j - 16;
                for (int i = 0; i < bm.Width; i++)
                {
                    int x = i - 16;
                    bm[i, j] = x * x + y * y <= radius2;
                }
            }
            return bm;
        }
    }
}