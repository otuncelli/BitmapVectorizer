// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;

namespace BitmapVectorizer
{
    public unsafe partial class Greymap
    {
        /* apply lowpass filter (an approximate Gaussian blur) to greymap.
           Lambda is the standard deviation of the kernel of the filter (i.e.,
           the approximate filter radius). */
        public void LowPassFilter(double lambda)
        {
            Ensure.IsGreaterThan(lambda, 0, nameof(lambda));

            /* calculate filter coefficients from given lambda */
            double B = 1 + 2 / (lambda * lambda);
            double c = B - Math.Sqrt(B * B - 1);
            double d = 1 - c;
            double f, g;

            int x, y;

            for (y = 0; y < Height; y++)
            {
                /* apply low-pass filter to row y */

                /* left-to-right */
                g = f = 0;
                for (x = 0; x < Width; x++)
                {
                    f = f * c + this[x, y] * d;
                    g = g * c + f * d;
                    GM_UPUT(x, y, (byte)g);
                }

                /* right-to-left */
                for (x = Width - 1; x >= 0; x--)
                {
                    f = f * c + this[x, y] * d;
                    g = g * c + f * d;
                    GM_UPUT(x, y, (byte)g);
                }

                /* left-to-right mop-up */
                for (x = 0; x < Width; x++)
                {
                    f *= c;
                    g = g * c + f * d;
                    if (f + g < 1 / 255d)
                    {
                        break;
                    }
                    GM_UPUT(x, y, (byte)(GM_UGET(x, y) + g));
                }
            }

            for (x = 0; x < Width; x++)
            {
                /* apply low-pass filter to column x */

                /* bottom-to-top */
                f = g = 0;
                for (y = 0; y < Height; y++)
                {
                    f = f * c + GM_UGET(x, y) * d;
                    g = g * c + f * d;
                    GM_UPUT(x, y, (byte)g);
                }

                /* top-to-bottom */
                for (y = Height - 1; y >= 0; y--)
                {
                    f = f * c + GM_UGET(x, y) * d;
                    g = g * c + f * d;
                    GM_UPUT(x, y, (byte)g);
                }

                /* bottom-to-top mop-up */
                for (y = 0; y < Height; y++)
                {
                    f *= c;
                    g = g * c + f * d;
                    if (f + g < 1 / 255d)
                    {
                        break;
                    }
                    GM_UPUT(x, y, (byte)(GM_UGET(x, y) + g));
                }
            }
        }

        /* apply highpass filter to greymap. */
        public void HighPassFilter(double lambda = 4)
        {
            Ensure.IsGreaterThan(lambda, 0, nameof(lambda));

            /* create a copy */
            using (Greymap copy = (Greymap)Clone())
            {
                /* apply lowpass filter to the copy */
                copy.LowPassFilter(lambda);

                /* subtract copy from original */
                int x, y;
                for (y = 0; y < Height; y++)
                {
                    for (x = 0; x < Width; x++)
                    {
                        int f = GM_UGET(x, y);
                        f -= copy.GM_UGET(x, y);
                        f += 128; /* normalize! */
                        f = MathHelper.Clamp(f, 0, 255);
                        GM_PUT(x, y, (byte)f);
                    }
                }
            }
        }

        //public void GaussianFilter()
        //{
        //    const int firstX = 2;
        //    const int firstY = 2;
        //    int lastX = Width - 3;
        //    int lastY = Height - 3;

        //    int[] kernel =
        //    {
        //         2,  4,  5,  4, 2,
        //         4,  9, 12,  9, 4,
        //         5, 12, 15, 12, 5,
        //         4,  9, 12,  9, 4,
        //         2,  4,  5,  4, 2
        //    };

        //    using (Greymap gm1 = (Greymap)Clone())
        //    {
        //        for (int y = firstY; y <= lastY; y++)
        //        {
        //            for (int x = firstX; x <= lastX; x++)
        //            {
        //                /* all other pixels */
        //                int index = 0;
        //                ulong sum = 0;
        //                for (int i = y - 2; i <= y + 2; i++)
        //                {
        //                    for (int j = x - 2; j <= x + 2; j++)
        //                    {
        //                        int weight = kernel[index++];
        //                        sum += gm1.GM_UGET(j, i) * (ulong)weight;
        //                    }
        //                }
        //                sum /= 159;
        //                GM_UPUT(x, y, (byte)sum);
        //            }
        //        }
        //    }
        //}

        //public PotraceBitmap SobelFilter(double dLow = 0.01, double dHigh = 0.65)
        //{
        //    Ensure.IsInRange(dLow, 0.0, 1.0, nameof(dLow));
        //    Ensure.IsInRange(dHigh, 0.0, 1.0, nameof(dHigh));
        //    Ensure.IsGreaterThanOrEqualTo(dHigh, dLow, nameof(dHigh));

        //    int[] sobelX =
        //    {
        //        -1,  0,  1 ,
        //        -2,  0,  2 ,
        //        -1,  0,  1
        //    };

        //    int[] sobelY =
        //    {
        //         1,  2,  1 ,
        //         0,  0,  0 ,
        //        -1, -2, -1
        //    };

        //    const int firstX = 1;
        //    const int firstY = 1;
        //    int lastX = Width - 2;
        //    int lastY = Height - 2;

        //    int iHigh = (int)Math.Round(dHigh * 255.0);
        //    int iLow = (int)Math.Round(dLow * 255.0);

        //    PotraceBitmap bm = new PotraceBitmap(Width, Height);
        //    for (int y = firstY; y <= lastY; y++)
        //    {
        //        for (int x = firstX; x <= lastX; x++)
        //        {
        //            /* ### SOBEL FILTERING #### */
        //            int sumX = 0, sumY = 0;
        //            int index = 0;
        //            for (int i = y - 1; i <= y + 1; i++)
        //            {
        //                for (int j = x - 1; j <= x + 1; j++, index++)
        //                {
        //                    byte b = GM_UGET(j, i);
        //                    sumX += b * sobelX[index];
        //                    sumY += b * sobelY[index];
        //                }
        //            }

        //            int sum = Math.Min(Math.Abs(sumX) + Math.Abs(sumY), 255);

        //            int dir = 0;
        //            if (sumX == 0)
        //            {
        //                if (sumY != 0)
        //                {
        //                    dir = 90;
        //                }
        //            }
        //            else
        //            {
        //                /*long slope = sumY*1024/sumX;*/
        //                int slope = (sumY << 10) / sumX;
        //                if (Math.Abs(slope) > 2472)  /*tan(67.5)*1024*/
        //                {
        //                    dir = 90;
        //                }
        //                else if (slope > 414) /*tan(22.5)*1024*/
        //                {
        //                    dir = 45;
        //                }
        //                else if (slope < -414) /*-tan(22.5)*1024*/
        //                {
        //                    dir = 135;
        //                }
        //            }

        //            /*### Get two adjacent pixels in edge direction ### */
        //            byte left, right;
        //            switch (dir)
        //            {
        //                case 0:
        //                    (left, right) = (GM_UGET(x - 1, y), GM_UGET(x + 1, y));
        //                    break;
        //                case 45:
        //                    (left, right) = (GM_UGET(x - 1, y + 1), GM_UGET(x + 1, y - 1));
        //                    break;
        //                case 90:
        //                    (left, right) = (GM_UGET(x, y - 1), GM_UGET(x, y + 1));
        //                    break;
        //                case 135:
        //                default:
        //                    (left, right) = (GM_UGET(x - 1, y - 1), GM_UGET(x + 1, y + 1));
        //                    break;
        //            }

        //            /*### Compare current value to adjacent pixels ### */
        //            /*### if less that either, suppress it ### */
        //            if (sum >= left && sum >= right)
        //            {
        //                if (sum >= iHigh || (sum >= iLow && FindEdge(x, y, iHigh)))
        //                {
        //                    bm.BM_UPUT(x, y, true);
        //                }
        //            }
        //        }
        //    }
        //    return bm;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private bool FindEdge(int x, int y, int highThreshold)
        //{
        //    for (int i = -1; i < 2; i++)
        //    {
        //        for (int j = -1; j < 2; j++)
        //        {
        //            if (i == 0 && j == 0)
        //            {
        //                continue;
        //            }

        //            if (GM_UGET(x + i, y + j) > highThreshold)
        //            {
        //                return true;
        //            }
        //        }
        //    }
        //    return false;
        //}
    }
}
