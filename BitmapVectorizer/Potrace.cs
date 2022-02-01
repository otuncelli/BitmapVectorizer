// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

#if NUMERICS
global using VECTOR = System.Numerics.Vector2;
global using FLOAT = System.Single;
#elif WINDOWS
global using VECTOR = System.Windows.Vector;
global using FLOAT = System.Double;
#else
global using VECTOR = BitmapVectorizer.Vector2F;
global using FLOAT = System.Single;
#endif

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("BitmapVectorizer.Tests")]

namespace BitmapVectorizer
{
    public static partial class Potrace
    {
        public const FLOAT AlphaMaxMin = 0;
        public const FLOAT AlphaMaxMax = (FLOAT)1.334;
        public const FLOAT AlphaMaxDef = 1;
        public const FLOAT OptToleranceMin = 0;
        public const FLOAT OptToleranceMax = 5;
        public const FLOAT OptToleranceDef = (FLOAT).2;

        public static TraceResult Trace(IPathList pathlist, FLOAT alphamax = AlphaMaxDef, FLOAT opttolerance = OptToleranceDef,
            IProgress<ProgressArgs>? progress = null, CancellationToken cancellationToken = default)
        {
            Ensure.IsNotNull(pathlist, nameof(pathlist));
            Ensure.IsInRange(alphamax, AlphaMaxMin, AlphaMaxMax, nameof(alphamax));
            Ensure.IsInRange(opttolerance, OptToleranceMin, OptToleranceMax, nameof(opttolerance));

            Path plist = (Path)pathlist;
            long cn = 0;
            long nn = 0;

            if (progress != null)
            {
                ProgressArgs args = ProgressArgs.Init(ProgressLevel.Tracing);
                progress.Report(args);
                plist.ForEach(path => nn += path.points.Length);
            }

            ParallelOptions po = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            void ThrowIfCancellationRequested() => cancellationToken.ThrowIfCancellationRequested();

            float progress1 = 0;

            /* call downstream function with each path */
            Parallel.ForEach(plist, po, path =>
            {
                ThrowIfCancellationRequested();
                CalcSums(path);
                ThrowIfCancellationRequested();
                CalcLon(path);
                ThrowIfCancellationRequested();
                BestPolygon(path);
                ThrowIfCancellationRequested();
                path.lon = null;
                AdjustVertices(path);
                ThrowIfCancellationRequested();
                path.sums = null;
                path.po = null;
                Smooth(path, alphamax);
                ThrowIfCancellationRequested();
                if (opttolerance > 0)
                {
                    OptiCurve(path, opttolerance);
                }

                if (progress != null && nn > 0)
                {
                    float prog = Interlocked.Add(ref cn, path.points.Length) / (float)nn;
                    prog = (float)Math.Round(prog, 2);
                    if (prog > progress1)
                    {
                        ProgressArgs args = new ProgressArgs(ProgressLevel.Tracing, prog);
                        progress.Report(args);
                        Interlocked.Exchange(ref progress1, prog);
                    }
                }
            });
            return new TraceResult(plist);
        }
    }
}
