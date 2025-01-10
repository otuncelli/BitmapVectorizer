// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System.Linq;

namespace BitmapVectorizer;

public sealed class TraceResult
{
    private readonly Path plist;

    internal Path? FirstPath => plist.FirstOrDefault();

    internal TraceResult(Path plist)
    {
        this.plist = plist;
    }
}
