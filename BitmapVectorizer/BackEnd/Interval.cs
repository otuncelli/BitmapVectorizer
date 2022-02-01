// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System.Runtime.CompilerServices;

namespace BitmapVectorizer
{
    internal sealed class Interval
    {
        public FLOAT Min
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        public FLOAT Max
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Interval(FLOAT min, FLOAT max)
        {
            Min = min;
            Max = max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Interval(FLOAT singleton) : this(singleton, singleton)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Extend(FLOAT x)
        {
            if (x < Min)
            {
                Min = x;
            }
            else if (x > Max)
            {
                Max = x;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIn(FLOAT x)
        {
            return Min <= x && x <= Max;
        }
    }
}
