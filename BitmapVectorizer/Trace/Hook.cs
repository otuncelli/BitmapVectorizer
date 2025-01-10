// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Runtime.CompilerServices;

namespace BitmapVectorizer;

internal sealed class Hook
{
    private Func<Path?>? get;
    private Action<Path?>? set;

    public Path? Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => get?.Invoke();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => set?.Invoke(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hook(Func<Path?> get, Action<Path?> set) => Set(get, set);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(Func<Path?> get, Action<Path?> set)
    {
        this.get = get;
        this.set = set;
    }
}
