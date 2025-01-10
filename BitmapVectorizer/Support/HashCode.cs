// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

#if !NETCOREAPP3_1_OR_GREATER
using System.Runtime.CompilerServices;

namespace BitmapVectorizer;

public static class HashCode
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Combine(int hash1, int hash2)
    {
        int result = hash1;
        result = unchecked(((result << 5) + result) ^ hash2);
        return result;
    }
}
#endif
