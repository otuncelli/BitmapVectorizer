// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System.Runtime.CompilerServices;

namespace BitmapVectorizer;

/* a private type for the result of opti_penalty */
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct Opti(FLOAT pen, in VECTOR c0, in VECTOR c1, FLOAT t, FLOAT s, FLOAT alpha)
{
    public readonly FLOAT pen = pen;
    public readonly VECTOR c0 = c0;
    public readonly VECTOR c1 = c1;
    public readonly FLOAT t = t;
    public readonly FLOAT s = s;
    public readonly FLOAT alpha = alpha;
}
