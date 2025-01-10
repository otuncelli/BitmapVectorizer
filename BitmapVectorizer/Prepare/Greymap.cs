// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BitmapVectorizer;

public sealed unsafe partial class Greymap : IDisposable, ICloneable
{
    #region Fields

    private readonly byte* ptr;
    private readonly byte[] buffer;
    private readonly int dy;
    private GCHandle handle;
    private bool disposed;

    #endregion

    #region Properties

    /* width and height, in pixels */
    public int Width
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public int Height
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    internal byte this[int x, int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GM_GET(x, y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => GM_PUT(x, y, value);
    }

    #endregion

    #region Constructor

    public Greymap(int width, int height)
    {
        Width = Ensure.IsGreaterThan(width, 0, nameof(width));
        Height = Ensure.IsGreaterThan(height, 0, nameof(height));
        dy = width;
        buffer = new byte[GetSize(dy, height)];
        handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        ptr = (byte*)handle.AddrOfPinnedObject().ToPointer();
    }

    #endregion

    #region Unsafe

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte* GetScanLine(int y)
    {
        return ptr + y * dy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte* GetIndex(int x, int y)
    {
        return GetScanLine(y) + x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte GM_UGET(int x, int y)
    {
        return *GetIndex(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte GM_UPUT(int x, int y, byte b)
    {
        return *GetIndex(x, y) = b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte GM_UINV(int x, int y)
    {
        return *GetIndex(x, y) = (byte)(255 - *GetIndex(x, y));
    }

    #endregion

    #region Safe

    /* calculate the size, in bytes, required for the data area of a
       greymap of the given dy and h. Assume h >= 0. Return -1 if the size
       does not fit into the ptrdiff_t type. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSize(int dy, int h)
    {
        dy = Math.Abs(dy);
        return checked(dy * h);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Check(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte GM_GET(int x, int y)
    {
        return Check(x, y) ? GM_UGET(x, y) : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GM_PUT(int x, int y, byte b)
    {
        if (Check(x, y))
        {
            GM_UPUT(x, y, b);
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GM_INV(int x, int y)
    {
        return Check(x, y) ? GM_UINV(x, y) : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GM_BOUND(int x, int m)
    {
        return x < 0 ? 0 : x >= m ? m - 1 : x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GM_BGET(int x, int y)
    {
        return Width == 0 || Height == 0 ? 0 : GM_UGET(GM_BOUND(x, Width), GM_BOUND(y, Height));
    }

    #endregion

    #region InterpolateLinear

    public Greymap InterpolateLinear(int s)
    {
        InterpolateLinear(s, out Greymap? gm, out PotraceBitmap? _, c: double.NaN);
        return gm!;
    }

    public PotraceBitmap InterpolateLinearBilevel(int s, double c = .45)
    {
        Ensure.IsNotNaN(c, nameof(c));
        Ensure.IsInRange(c, 0.0, 1.0, nameof(c));
        InterpolateLinear(s, out Greymap? _, out PotraceBitmap? bm, c: c);
        return bm!;
    }

    /* scale greymap by factor s, using linear interpolation. If
       bilevel=0, return a pointer to a greymap_t. If bilevel=1, return a
       pointer to a potrace_bitmap_t and use cutoff threshold c (0=black,
       1=white).  On error, return NULL with errno set. */
    private void InterpolateLinear(int s, out Greymap? gm, out PotraceBitmap? bm, double c = double.NaN)
    {
        if (!double.IsNaN(c))
        {
            Ensure.IsInRange(c, 0.0, 1.0, nameof(c));
        }
        Ensure.IsGreaterThan(s, 1, nameof(s));

        bool bilevel = !double.IsNaN(c);
        double c1;

        /* allocate output bitmap/greymap */
        if (bilevel)
        {
            bm = new PotraceBitmap(Width * s, Height * s);
            c1 = c * 255;
            gm = null;
        }
        else
        {
            gm = new Greymap(Width * s, Height * s);
            bm = null;
            c1 = double.NaN;
        }

        /* interpolate */
        try
        {
            int x, y;
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    int p00 = GM_BGET(i, j);
                    int p01 = GM_BGET(i, j + 1);
                    int p10 = GM_BGET(i + 1, j);
                    int p11 = GM_BGET(i + 1, j + 1);

                    if (bilevel)
                    {
                        /* treat two special cases which are very common */
                        if (p00 < c1 && p01 < c1 && p10 < c1 && p11 < c1)
                        {
                            for (x = 0; x < s; x++)
                            {
                                for (y = 0; y < s; y++)
                                {
                                    bm!.BM_UPUT(i * s + x, j * s + y, true);
                                }
                            }
                            continue;
                        }

                        if (p00 >= c1 && p01 >= c1 && p10 >= c1 && p11 >= c1)
                        {
                            continue;
                        }
                    }

                    /* the general case */
                    for (x = 0; x < s; x++)
                    {
                        double xx = x / (double)s;
                        double p0 = p00 * (1 - xx) + p10 * xx;
                        double p1 = p01 * (1 - xx) + p11 * xx;
                        for (y = 0; y < s; y++)
                        {
                            double yy = y / (double)s;
                            double av = p0 * (1 - yy) + p1 * yy;

                            if (bilevel)
                            {
                                bm!.BM_UPUT(i * s + x, j * s + y, av < c1);
                            }
                            else
                            {
                                gm!.GM_UPUT(i * s + x, j * s + y, (byte)av);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            gm?.Dispose();
            bm?.Dispose();
            throw;
        }
    }

    #endregion

    #region InterpolateCubic

    public Greymap InterpolateCubic(int s)
    {
        InterpolateCubic(s, out Greymap? gm, out PotraceBitmap? _, c: double.NaN);
        return gm!;
    }

    public PotraceBitmap InterpolateCubicBilevel(int s, double c = .45)
    {
        Ensure.IsNotNaN(c, nameof(c));
        Ensure.IsInRange(c, 0.0, 1.0, nameof(c));
        InterpolateCubic(s, out Greymap? _, out PotraceBitmap? bm, c: c);
        return bm!;
    }

    /* same as interpolate_linear, except use cubic interpolation (slower
       and better). */
    private void InterpolateCubic(int s, out Greymap? gm, out PotraceBitmap? bm, double c = double.NaN)
    {
        if (!double.IsNaN(c))
        {
            Ensure.IsInRange(c, 0.0, 1.0, nameof(c));
        }
        Ensure.IsInRange(c, 0.0, 1.0, nameof(c));
        Ensure.IsGreaterThan(s, 1, nameof(s));

        double[,] poly = new double[4, s]; /* poly[s][4]: fixed interpolation polynomials */
        double[,] window = new double[4, s]; /* window[s][4]: current state */
        bool bilevel = !double.IsNaN(c);
        double[] p = new double[4]; /* four current points */
        double c1, t, v;
        int x, y, i, j, k, l;

        if (bilevel)
        {
            bm = new PotraceBitmap(Width * s, Height * s);
            c1 = c * 255;
            gm = null;
        }
        else
        {
            gm = new Greymap(Width * s, Height * s);
            c1 = double.NaN;
            bm = null;
        }

        try
        {
            /* pre-calculate interpolation polynomials */
            for (k = 0; k < s; k++)
            {
                t = k / (double)s;
                poly[0, k] = 0.5 * t * (t - 1) * (1 - t);
                poly[1, k] = -(t + 1) * (t - 1) * (1 - t) + 0.5 * (t - 1) * (t - 2) * t;
                poly[2, k] = 0.5 * (t + 1) * t * (1 - t) - t * (t - 2) * t;
                poly[3, k] = 0.5 * t * (t - 1) * t;
            }

            /* interpolate */
            for (y = 0; y < Height; y++)
            {
                x = 0;
                for (i = 0; i < 4; i++)
                {
                    for (j = 0; j < 4; j++)
                    {
                        p[j] = GM_BGET(x + i - 1, y + j - 1);
                    }
                    for (k = 0; k < s; k++)
                    {
                        window[i, k] = 0.0;
                        for (j = 0; j < 4; j++)
                        {
                            window[i, k] += poly[j, k] * p[j];
                        }
                    }
                }
                while (true)
                {
                    for (l = 0; l < s; l++)
                    {
                        for (k = 0; k < s; k++)
                        {
                            v = 0.0;
                            for (i = 0; i < 4; i++)
                            {
                                v += window[i, k] * poly[i, l];
                            }
                            if (bilevel)
                            {
                                bm!.BM_PUT(x * s + l, y * s + k, v < c1);
                            }
                            else
                            {
                                gm!.GM_PUT(x * s + l, y * s + k, (byte)v);
                            }
                        }
                    }
                    x++;
                    if (x >= Width)
                    {
                        break;
                    }
                    for (i = 0; i < 3; i++)
                    {
                        for (k = 0; k < s; k++)
                        {
                            window[i, k] = window[i + 1, k];
                        }
                    }
                    i = 3;
                    for (j = 0; j < 4; j++)
                    {
                        p[j] = GM_BGET(x + i - 1, y + j - 1);
                    }
                    for (k = 0; k < s; k++)
                    {
                        window[i, k] = 0.0;
                        for (j = 0; j < 4; j++)
                        {
                            window[i, k] += poly[j, k] * p[j];
                        }
                    }
                }
            }
        }
        catch
        {
            gm?.Dispose();
            bm?.Dispose();
            throw;
        }
    }

    #endregion

    #region Threshold

    /* Convert greymap to bitmap by using cutoff threshold. */
    public PotraceBitmap Threshold(double c = .45)
    {
        Ensure.IsInRange(c, 0.0, 1.0, nameof(c));

        /* allocate output bitmap */
    PotraceBitmap bm = new PotraceBitmap(Width, Height);
        try
        {
            /* thresholding */
            double c1 = c * 255;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bm.BM_UPUT(x, y, GM_UGET(x, y) < c1);
                }
            }
        }
        catch
        {
            bm.Dispose();
            throw;
        }
        return bm;
    }

    #endregion

    #region Save as .pgm

    /* Write a pgm stream, either P2 or (if raw is true) P5 format. Include
     * one-line comment if non-NULL. Mode determines how out-of-range
     * color values are converted. Gamma is the desired gamma correction,
     * if any (set to 2.2 if the image is to look optimal on a CRT monitor,
     * 2.8 for LCD). Set to 1.0 for no gamma correction */
    public void SaveAsPgm(Stream stream, bool raw = true, GreymapMode mode = GreymapMode.Positive, double gamma = 1.0)
    {
        Ensure.IsNotNull(stream, nameof(stream));
        Ensure.IsInRange(gamma, 0.0, 1.0, nameof(gamma));

        int v;
        int[] gammatable = new int[256];

        /* prepare gamma correction lookup table */
        if (gamma != 1.0)
        {
            gammatable[0] = 0;
            for (v = 1; v < 256; v++)
            {
                gammatable[v] = (int)(255 * Math.Exp(Math.Log(v / 255.0) / gamma) + .5);
            }
        }
        else
        {
            for (v = 0; v < 256; v++)
            {
                gammatable[v] = v;
            }
        }

        byte[] header = Encoding.ASCII.GetBytes($"{(raw ? "P5" : "P2")}\n{Width} {Height} 255\n");
        stream.Write(header, 0, header.Length);
        for (int y = Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < Width; x++)
            {
                v = GM_UGET(x, y);
                switch (mode)
                {
                    case GreymapMode.NonZero:
                        if (v > 255)
                        {
                            v = 510 - v;
                        }
                        if (v < 0)
                        {
                            v = 0;
                        }
                        break;
                    case GreymapMode.Odd:
                        v = MathHelper.Mod(v, 510);
                        if (v > 255)
                        {
                            v = 510 - v;
                        }
                        break;
                    case GreymapMode.Negative:
                        v = 510 - v;
                        v = MathHelper.Clamp(v, 0, 255);
                        break;
                    case GreymapMode.Positive:
                    default:
                        v = MathHelper.Clamp(v, 0, 255);
                        break;
                }
                v = gammatable[v];
                if (raw)
                {
                    stream.WriteByte((byte)v);
                }
                else
                {
                    stream.WriteByte((byte)v);
                    if (x == Width - 1)
                    {
                        stream.WriteByte((byte)'\n');
                    }
                }
            }
        }
    }

    #endregion

    #region Static Methods

    public static Greymap FromRgbx(IntPtr scan0, int width, int height)
    {
        Ensure.IsNotNull(scan0, nameof(scan0));
        Ensure.IsGreaterThan(width, 0, nameof(width));
        Ensure.IsGreaterThan(height, 0, nameof(height));

        Greymap gm = new Greymap(width, height);
        try
        {
            unsafe
            {
                int* src = (int*)(void*)scan0;
                for (int y = 0; y < height; y++)
                {
                    int* row = src + y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int t = row[x];
                        t = (int)(((t >> 16) & 0xff) * .3
                            + ((t >> 8) & 0xff) * .59
                            + ((t & 0xff) * .11));
                        gm.GM_UPUT(x, height - y - 1, (byte)t);
                    }
                }
            }
        }
        catch
        {
            gm.Dispose();
            throw;
        }
        return gm;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!disposed)
        {
            handle.Free();
            *ptr = 0;
            disposed = true;
        }
    }

    #endregion

    #region ICloneable

    public object Clone()
    {
        Greymap gm = new Greymap(Width, Height);
        try
        {
            Array.Copy(buffer, gm.buffer, buffer.Length);
        }
        catch
        {
            gm.Dispose();
            throw;
        }
        return gm;
    }

    #endregion
}
