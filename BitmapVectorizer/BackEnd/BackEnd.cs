// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace BitmapVectorizer;

public abstract class BackEnd
{
    #region Properties

    public abstract BackEndType Type { get; }

    public abstract bool MultiPage { get; }

    public abstract string Name { get; }

    public abstract string Extension { get; }

    public abstract bool OptiCurve { get; }

    public virtual FLOAT PaperWidth
    {
        get => FLOAT.NaN;
        set => throw new NotImplementedException();
    }

    public virtual FLOAT PaperHeight
    {
        get => FLOAT.NaN;
        set => throw new NotImplementedException();
    }

    public virtual FLOAT Sx
    {
        get => FLOAT.NaN;
        set => throw new NotImplementedException();
    }

    public virtual FLOAT Sy
    {
        get => FLOAT.NaN;
        set => throw new NotImplementedException();
    }

    public virtual FLOAT Rx
    {
        get => FLOAT.NaN;
        set => throw new NotImplementedException();
    }

    public virtual FLOAT Ry
    {
        get => FLOAT.NaN;
        set => throw new NotImplementedException();
    }

    #endregion

    #region Methods

    internal void CalcDimensions(ImageInfo imginfo, Path plist)
    {
        Trans trans = imginfo.Trans;
        IReadOnlyList<FLOAT> tbb = trans.Bb;
        if (imginfo.Tight)
        {
            trans.Tighten(plist);
        }

        /* sx/rx is just an alternate way to specify width; sy/ry is just an
           alternate way to specify height. */
        if (Type == BackEndType.PixelBased)
        {
            if (FLOAT.IsNaN(imginfo.Width) && !FLOAT.IsNaN(Sx))
            {
                imginfo.Width = tbb[0] * Sx;
            }
            if (FLOAT.IsNaN(imginfo.Height) && !FLOAT.IsNaN(Sy))
            {
                imginfo.Height = tbb[1] * Sy;
            }
        }
        else
        {
            const FLOAT dpi = 72.0f;
            if (FLOAT.IsNaN(imginfo.Width) && !FLOAT.IsNaN(Rx))
            {
                imginfo.Width = tbb[0] / Rx * dpi;
            }
            if (FLOAT.IsNaN(imginfo.Height) && !FLOAT.IsNaN(Ry))
            {
                imginfo.Height = tbb[1] / Ry * dpi;
            }
        }

        /* if one of width/height is specified, use stretch to determine the other */
        if (FLOAT.IsNaN(imginfo.Width) && !FLOAT.IsNaN(imginfo.Height))
        {
            imginfo.Width = imginfo.Height / tbb[1] * tbb[0] / imginfo.Stretch;
        }
        else if (!FLOAT.IsNaN(imginfo.Width) && FLOAT.IsNaN(imginfo.Height))
        {
            imginfo.Height = imginfo.Width / tbb[0] * tbb[1] * imginfo.Stretch;
        }

        int default_scaling = 0;
        /* if width and height are still variable, tenatively use the
           default scaling factor of 72dpi (for dimension-based backends) or
           1 (for pixel-based backends). For fixed-size backends, this will
           be adjusted later to fit the page. */
        if (FLOAT.IsNaN(imginfo.Width) && FLOAT.IsNaN(imginfo.Height))
        {
            imginfo.Width = tbb[0];
            imginfo.Height = tbb[1] * imginfo.Stretch;
            default_scaling = 1;
        }

        /* apply scaling */
        trans.ScaleToSize(imginfo.Width, imginfo.Height);

        /* apply rotation, and tighten the bounding box again, if necessary */
        if (!FLOAT.IsNaN(imginfo.Angle) && imginfo.Angle != 0)
        {
            trans.Rotate(imginfo.Angle);
            if (imginfo.Tight)
            {
                trans.Tighten(plist);
            }
        }

        /* for fixed-size backends, if default scaling was in effect,
           further adjust the scaling to be the "best fit" for the given
           page size and margins. */
        if (Type == BackEndType.Fixed)
        {
            if (default_scaling > 0)
            {
                /* try to squeeze it between margins */
                FLOAT maxwidth = FLOAT.NaN;
                FLOAT maxheight = FLOAT.NaN;
                if (!FLOAT.IsNaN(imginfo.Lmar) && !FLOAT.IsNaN(imginfo.Rmar))
                {
                    maxwidth = PaperWidth - imginfo.Lmar - imginfo.Rmar;
                }
                if (!FLOAT.IsNaN(imginfo.Tmar) && !FLOAT.IsNaN(imginfo.Bmar))
                {
                    maxheight = PaperHeight - imginfo.Tmar - imginfo.Bmar;
                }

                if (FLOAT.IsNaN(maxwidth) && FLOAT.IsNaN(maxheight))
                {
                    maxwidth = Math.Max(PaperWidth - 144, PaperWidth * .75f);
                    maxheight = Math.Max(PaperHeight - 144, PaperHeight * .75f);
                }

                FLOAT sc;
                if (FLOAT.IsNaN(maxwidth))
                {
                    sc = maxheight / tbb[1];
                }
                else if (FLOAT.IsNaN(maxheight))
                {
                    sc = maxwidth / tbb[0];
                }
                else
                {
                    sc = Math.Min(maxwidth / tbb[0], maxheight / tbb[1]);
                }

                /* re-scale coordinate system */
                imginfo.Width *= sc;
                imginfo.Height *= sc;
                trans.Rescale(sc);
            }

            /* adjust margins */
            if (FLOAT.IsNaN(imginfo.Lmar) && FLOAT.IsNaN(imginfo.Rmar))
            {
                imginfo.Lmar = (PaperWidth - tbb[0]) / 2;
            }
            else if (FLOAT.IsNaN(imginfo.Lmar))
            {
                imginfo.Lmar = PaperWidth - tbb[0] - imginfo.Rmar;
            }
            else if (!FLOAT.IsNaN(imginfo.Lmar) && !FLOAT.IsNaN(imginfo.Rmar))
            {
                imginfo.Lmar += (PaperWidth - tbb[0] - imginfo.Lmar - imginfo.Rmar) / 2;
            }

            if (FLOAT.IsNaN(imginfo.Bmar) && FLOAT.IsNaN(imginfo.Tmar))
            {
                imginfo.Bmar = (PaperHeight - tbb[1]) / 2;
            }
            else if (FLOAT.IsNaN(imginfo.Bmar))
            {
                imginfo.Bmar = PaperHeight - tbb[1] - imginfo.Tmar;
            }
            else if (!FLOAT.IsNaN(imginfo.Bmar) && !FLOAT.IsNaN(imginfo.Tmar))
            {
                imginfo.Bmar += (PaperHeight - tbb[1] - imginfo.Bmar - imginfo.Tmar) / 2;
            }
        }
        else
        {
            /* adjust margins */
            if (FLOAT.IsNaN(imginfo.Lmar))
            {
                imginfo.Lmar = 0;
            }

            if (FLOAT.IsNaN(imginfo.Rmar))
            {
                imginfo.Rmar = 0;
            }

            if (FLOAT.IsNaN(imginfo.Bmar))
            {
                imginfo.Bmar = 0;
            }

            if (FLOAT.IsNaN(imginfo.Tmar))
            {
                imginfo.Tmar = 0;
            }
        }
    }

    public abstract void Save(Stream output, TraceResult trace, ImageInfo imginfo, CancellationToken cancellationToken = default);

    #endregion
}
