// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System.Runtime.CompilerServices;

namespace BitmapVectorizer
{
    /* a private type for the result of opti_penalty */
    internal readonly struct Opti
    {
        public readonly FLOAT pen;
        public readonly VECTOR c0;
        public readonly VECTOR c1;
        public readonly FLOAT t;
        public readonly FLOAT s;
        public readonly FLOAT alpha;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Opti(FLOAT pen, in VECTOR c0, in VECTOR c1, FLOAT t, FLOAT s, FLOAT alpha)
        {
            this.pen = pen;
            this.c0 = c0;
            this.c1 = c1;
            this.t = t;
            this.s = s;
            this.alpha = alpha;
        }
    }
}
