// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;

namespace BitmapVectorizer
{
    public sealed class ImageInfo
    {
        public int PixWidth { get; }

        public int PixHeight { get; }

        public FLOAT Width { get; set; } = FLOAT.NaN;

        public FLOAT Height { get; set; } = FLOAT.NaN;

        public FLOAT Lmar { get; set; } = FLOAT.NaN;

        public FLOAT Rmar { get; set; } = FLOAT.NaN;

        public FLOAT Tmar { get; set; } = FLOAT.NaN;

        public FLOAT Bmar { get; set; } = FLOAT.NaN;

        public FLOAT Stretch { get; set; } = 1.0f;

        public FLOAT Angle { get; set; } = 0.0f;

        public bool Tight { get; set; }

        internal Trans Trans { get; }

        internal ImageInfo(int pixWidth, int pixHeight)
        {
            ///* we take care of a special case: if one of the image dimensions is
            //   0, we change it to 1. Such an image is empty anyway, so there
            //   will be 0 paths in it. Changing the dimensions avoids division by
            //   0 error in calculating scaling factors, bounding boxes and
            //   such. This doesn't quite do the right thing in all cases, but it
            //   is better than causing overflow errors or "nan" output in
            //   backends.  Human users don't tend to process images of size 0
            //   anyway; they might occur in some pipelines. */
            PixWidth = Math.Max(pixWidth, 1);
            PixHeight = Math.Max(pixHeight, 1);
            Trans = new Trans(PixWidth, PixHeight);
        }
    }
}
