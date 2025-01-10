// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

namespace BitmapVectorizer;

public enum TurnPolicy
{
    /// <summary>
    /// Prefers to connect black (foreground) components.
    /// </summary>
    Black,

    /// <summary>
    /// Prefers to connect white (background) components.
    /// </summary>
    White,

    /// <summary>
    /// Always take a left turn.
    /// </summary>
    Left,

    /// <summary>
    /// Always take a right turn.
    /// </summary>
    Right,

    /// <summary>
    /// Prefers to connect the color (black or white) that occurs 
    /// least frequently in a local neighborhood of the current position.
    /// </summary>
    Minority,

    /// <summary>
    /// Prefers to connect the color (black or white) that occurs 
    /// most frequently in a local neighborhood of the current position.
    /// </summary>
    Majority,

    /// <summary>
    /// Choose randomly.
    /// </summary>
    Random
}
