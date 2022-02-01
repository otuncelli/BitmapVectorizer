// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

namespace BitmapVectorizer
{
    public enum ProgressLevel
    {
        GeneratingPathList,
        Tracing
    }

    public class ProgressArgs
    {
        public float Progress { get; }
        public ProgressLevel Level { get; }

        internal ProgressArgs(ProgressLevel level, float progress)
        {
            Level = level;
            Progress = progress;
        }

        public static ProgressArgs Init(ProgressLevel level) => new(level, 0);
    }
}
