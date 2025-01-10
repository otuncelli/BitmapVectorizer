// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

namespace BitmapVectorizer;

/// <summary>
/// Modes for cutting off out-of-range values. The following names
/// refer to winding numbers. I.e., make a pixel black if winding
/// number is nonzero, odd, or positive, respectively. We assume that 0
/// winding number corresponds to white (255).
/// </summary>
public enum GreymapMode
{
    Positive,
    Negative,
    NonZero,
    Odd,
}
