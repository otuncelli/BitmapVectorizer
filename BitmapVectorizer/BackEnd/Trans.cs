// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Collections.Generic;

namespace BitmapVectorizer;

/* calculations with coordinate transformations and bounding boxes */
internal sealed class Trans(FLOAT w, FLOAT h)
{
    public IReadOnlyList<FLOAT> Bb => bb;

    public IReadOnlyList<FLOAT> Orig => orig;

    public IReadOnlyList<FLOAT> X => x;

    public IReadOnlyList<FLOAT> Y => y;

    public FLOAT ScaleX => scalex;

    public FLOAT ScaleY => scaley;

    private readonly FLOAT[] bb = [w, h];   /* dimensions of bounding box */
    private readonly FLOAT[] orig = new FLOAT[2]; /* origin relative to bounding box */
    private readonly FLOAT[] x = [1, 0];    /* basis vector for the "x" direction */
    private readonly FLOAT[] y = [0, 1];    /* basis vector for the "y" direction */
    private FLOAT scalex = 1;
    private FLOAT scaley = 1;

    /* apply a coordinate transformation to a point */
    public Trans(int pixWidth, int pixHeight) : this((FLOAT)pixWidth, pixHeight)
    {
    }

    private static Trans Copy(Trans trans)
    {
        var t = new Trans(trans.bb[0], trans.bb[1]);
        Array.Copy(trans.orig, t.orig, 2);
        Array.Copy(trans.x, t.x, 2);
        Array.Copy(trans.y, t.y, 2);
        t.scalex = trans.scalex;
        t.scaley = trans.scaley;
        return t;
    }

    /* rotate the coordinate system counterclockwise by alpha degrees. The
       new bounding box will be the smallest box containing the rotated
       old bounding box */
    public void Rotate(FLOAT alpha)
    {
        Trans t = Copy(this);

        FLOAT theta = (FLOAT)(alpha / 180 * Math.PI);
        (FLOAT s, FLOAT c) = MathHelper.SinCos(theta);

        /* apply the transformation matrix to the sides of the bounding box */
        FLOAT _x0 = c * t.bb[0];
        FLOAT _x1 = s * t.bb[0];
        FLOAT _y0 = -s * t.bb[1];
        FLOAT _y1 = c * t.bb[1];

        /* determine new bounding box, and origin of old bb within new bb */
        bb[0] = Math.Abs(_x0) + Math.Abs(_y0);
        bb[1] = Math.Abs(_x1) + Math.Abs(_y1);
        FLOAT o0 = -Math.Min(_x0, 0) - Math.Min(_y0, 0);
        FLOAT o1 = -Math.Min(_x1, 0) - Math.Min(_y1, 0);

        orig[0] = o0 + c * t.orig[0] - s * t.orig[1];
        orig[1] = o1 + s * t.orig[0] + c * t.orig[1];

        x[0] = c * t.x[0] - s * t.x[1];
        x[1] = s * t.x[0] + c * t.x[1];
        y[0] = c * t.y[0] - s * t.y[1];
        y[1] = s * t.y[0] + c * t.y[1];
    }

    /* rescale the coordinate system to size w x h */
    public void ScaleToSize(FLOAT w, FLOAT h)
    {
        FLOAT xsc = w / bb[0];
        FLOAT ysc = h / bb[1];
        bb[0] = w;
        bb[1] = h;
        orig[0] *= xsc;
        orig[1] *= ysc;
        x[0] *= xsc;
        x[1] *= ysc;
        y[0] *= xsc;
        y[1] *= ysc;
        scalex *= xsc;
        scaley *= ysc;

        if (w < 0)
        {
            orig[0] -= w;
            bb[0] = -w;
        }
        if (h < 0)
        {
            orig[1] -= h;
            bb[1] = -h;
        }
    }

    /* rescale the coordinate system r by factor sc >= 0. */
    public void Rescale(FLOAT sc)
    {
        for (int i = 0; i < 2; i++)
        {
            bb[i] *= sc;
            orig[i] *= sc;
            x[i] *= sc;
            y[i] *= sc;
        }
        scalex *= sc;
        scaley *= sc;
    }

    /* adjust the bounding box to the actual vector outline */
    public void Tighten(Path plist)
    {
        /* if pathlist is empty, do nothing */
        if (plist?.FCurves == null || plist.FCurves.Count == 0)
        {
            return;
        }

        FLOAT dirx, diry;
        for (int j = 0; j < 2; j++)
        {
            dirx = x[j];
            diry = y[j];
            VECTOR dir = new VECTOR(dirx, diry);
            Interval i = plist.SetLimits(dir);
            if (i.Min == i.Max)
            {
                /* make the extent non-zero to avoid later division by zero errors */
                i.Max = i.Min + .5f;
                i.Min -= .5f;
            }
            bb[j] = i.Max - i.Min;
            orig[j] = -i.Min;
        }
    }
}
