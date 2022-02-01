// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace BitmapVectorizer
{
    /* Internal bitmap format. The n-th scanline starts at scanline(n) =
       (map + n*dy). Raster data is stored as a sequence of ulongs
       (NOT bytes). The leftmost bit of scanline n is the most significant
       bit of scanline(n)[0]. */
    public sealed unsafe partial class PotraceBitmap : IDisposable, ICloneable
    {
        #region Fields

        public const int TurdSizeMin = 0, TurdSizeMax = 1000, TurdSizeDef = 2;
        private const int BM_WORDSIZE = sizeof(ulong);
        private const int BM_WORDBITS = 8 * BM_WORDSIZE;
        private const ulong BM_HIBIT = 1ul << (BM_WORDBITS - 1);
        private const ulong BM_ALLBITS = ~0ul;
        private readonly ulong* ptr;
        private readonly int dy;
        private readonly ulong[] buffer;
        private Random? rng;
        private bool disposed;
        private GCHandle handle;

        #endregion

        #region Properties

        /* width and height, in pixels */
        public int Width
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public int Height
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public ImageInfo Info => new(Width, Height);

        internal bool this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BM_GET(x, y);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => BM_PUT(x, y, value);
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates an all-white bitmap.
        /// </summary>
        public PotraceBitmap(int width, int height)
        {
            Width = Ensure.IsGreaterThan(width, 0, nameof(width));
            Height = Ensure.IsGreaterThan(height, 0, nameof(height));
            dy = GetDy(width);
            buffer = new ulong[GetSize(dy, height)];
            handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            ptr = (ulong*)handle.AddrOfPinnedObject().ToPointer();
        }

        #endregion

        #region Public Methods

        public void Invert()
        {
            for (var y = 0; y < Height; y++)
            {
                ulong* line = GetScanLine(y);
                for (var i = 0; i < dy; i++)
                {
                    line[i] ^= BM_ALLBITS;
                }
            }
        }

        public void Enclose()
        {
            for (var y = 0; y < Height; y++)
            {
                if (y == 0 || y == Height - 1)
                {
                    ulong* line = GetScanLine(y);
                    for (var i = 0; i < dy; i++)
                    {
                        line[i] |= BM_ALLBITS;
                    }
                    continue;
                }
                SetBlackUnsafe(0, y);
                SetBlackUnsafe(Width - 1, y);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="turdsize">
        /// The *turdsize* parameter can be used to "despeckle" the bitmap to be
        /// traced, by removing all curves whose enclosed area is below the given
        /// threshold. The current default for the *turdsize* parameter is 2; its
        /// useful range is from 0 to infinity.
        /// </param>
        /// <param name="turnpolicy">
        /// The *turnpolicy* parameter determines how to resolve ambiguities during 
        /// decomposition of bitmaps into paths.
        /// </param>
        /// <param name="alphamax">
        /// The *alphamax* parameter is a threshold for the detection of corners.
        /// It controls the smoothness of the traced curve. The current default is
        /// 1.0; useful range of this parameter is from 0.0 (polygon) to 1.3333
        /// (no corners).
        /// </param>
        /// <param name="opttolerance">
        /// The *opttolerance* parameter defines the amount of error allowed in
        /// this simplification. The current default is 0.2. Larger values tend to
        /// decrease the number of segments, at the expense of less accuracy. The
        /// useful range is from 0 to infinity, although in practice one would
        /// hardly choose values greater than 1 or so. For most purposes, the
        /// default value is a good tradeoff between space and accuracy.
        /// </param>
        /// <returns></returns>
        public TraceResult? Trace(int turdsize = TurdSizeDef, TurnPolicy turnpolicy = TurnPolicy.Minority,
            FLOAT alphamax = Potrace.AlphaMaxDef, FLOAT opttolerance = Potrace.OptToleranceDef,
            IProgress<ProgressArgs>? progress = null, CancellationToken cancellationToken = default)
        {
            IPathList? plist = GetPathList(turdsize, turnpolicy, progress, cancellationToken);
            if (plist is null) { return null; }
            TraceResult trace = Potrace.Trace(plist, alphamax, opttolerance, progress, cancellationToken);
            return trace;
        }

        /* Decompose the given bitmap into paths. */
        public IPathList? GetPathList(int turdsize = TurdSizeDef, TurnPolicy turnpolicy = TurnPolicy.Minority,
            IProgress<ProgressArgs>? progress = null, CancellationToken cancellationToken = default)
        {
            Ensure.IsInRange(turdsize, TurdSizeMin, TurdSizeMax, nameof(turdsize));

            cancellationToken.ThrowIfCancellationRequested();

            if (progress != null)
            {
                ProgressArgs args = ProgressArgs.Init(ProgressLevel.GeneratingPathList);
                progress.Report(args);
            }

            Path? plist = null;

            /* used to speed up appending to linked list */
            Hook plist_hook = new Hook(() => plist, value => plist = value);

            using (PotraceBitmap bm1 = (PotraceBitmap)Clone())
            {
                if (turnpolicy == TurnPolicy.Random)
                {
                    bm1.rng = new Random();
                }

                /* be sure the byte padding on the right is set to 0, as the fast
                   pixel search below relies on it */
                bm1.ClearExcess();
                int h = bm1.Height;
                int x = 0;
                int y = h - 1;
                float progress1 = 0.0f;

                while (bm1.FindNext(ref x, ref y))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    /* calculate the sign by looking at the original */
                    bool sign = BM_UGET(x, y);

                    /* calculate the path */
                    Path path = bm1.FindPath(x, y + 1, sign, turnpolicy, cancellationToken);

                    /* update buffered image */
                    bm1.XorPath(path);

                    /* if it's a turd, eliminate it, else append it to the list */
                    if (path.area > turdsize)
                    {
                        path.InsertBeforeHook(plist_hook);
                    }

                    /* to be sure */
                    if (progress != null && h > 0)
                    {
                        float prog = (float)Math.Round(1 - y / (float)h, 2);
                        if (prog > progress1)
                        {
                            ProgressArgs args = new ProgressArgs(ProgressLevel.GeneratingPathList, prog);
                            progress.Report(args);
                            progress1 = prog;
                        }
                    }
                }
                bm1.PathListToTree(plist, cancellationToken);
            }
            return plist;
        }

        public void SaveAsPbm(Stream stream)
        {
            Ensure.IsNotNull(stream, nameof(stream));

            byte[] bytes = Encoding.ASCII.GetBytes($"P4\n{Width} {Height}\n");
            stream.Write(bytes, 0, bytes.Length);
            int bpr = (Width + 7) / 8;
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int i = 0; i < bpr; i++)
                {
                    byte c = (byte)((*GetIndex(i * 8, y) >> (8 * (BM_WORDSIZE - 1 - (i % BM_WORDSIZE)))) & 0xff);
                    stream.WriteByte(c);
                }
            }
        }

        #endregion

        #region Private Methods

        /* Give a tree structure to the given path list, based on "insideness"
           testing. I.e., path A is considered "below" path B if it is inside
           path B. The input pathlist is assumed to be ordered so that "outer"
           paths occur before "inner" paths. The tree structure is stored in
           the "childlist" and "sibling" components of the path_t
           structure. The linked list structure is also changed so that
           negative path components are listed immediately after their
           positive parent.  Note: some backends may ignore the tree
           structure, others may use it e.g. to group path components. We
           assume that in the input, point 0 of each path is an "upper left"
           corner of the path, as returned by bm_to_pathlist. This makes it
           easy to find an "interior" point. The bm argument should be a
           bitmap of the correct size (large enough to hold all the paths),
           and will be used as scratch space. Return 0 on success or -1 on
           error with errno set. */
        private void PathListToTree(Path? plist, CancellationToken ctoken = default)
        {
            ctoken.ThrowIfCancellationRequested();

            Clear();
            plist?.ForEach(p =>
            {
                ctoken.ThrowIfCancellationRequested();

                p.Sibling = p.Next;
                p.ChildList = null;
            });

            Path? cur;
            Path? heap = plist;

            /* the heap holds a list of lists of paths. Use "childlist" field
            for outer list, "next" field for inner list. Each of the sublists
            is to be turned into a tree. This code is messy, but it is
            actually fast. Each path is rendered exactly once. We use the
            heap to get a tail recursive algorithm: the heap holds a list of
            pathlists which still need to be transformed. */

            while (heap != null)
            {
                ctoken.ThrowIfCancellationRequested();

                // unlink first sublist
                cur = heap;
                heap = heap.ChildList;
                cur.ChildList = null;

                // unlink first Path
                Path head = cur;
                cur = cur.Next;
                head.Next = null;

                // render Path
                XorPath(head);
                Bbox bbox = SetBboxPath(head);

                /* now do insideness test for each element of cur; append it to
                   head->childlist if it's inside head, else append it to
                   head->next. */
                Path tmpHead = head;
                Hook hook_in = new Hook(() => tmpHead.ChildList, path => tmpHead.ChildList = path);
                Hook hook_out = new Hook(() => tmpHead.Next, path => tmpHead.Next = path);

                for (Path? p = cur; p != null; p = cur)
                {
                    ctoken.ThrowIfCancellationRequested();

                    cur = p.Next;
                    p.Next = null;

                    if (p.points[0].Y <= bbox.Y0)
                    {
                        p.InsertBeforeHook(hook_out);
                        /* append the remainder of the list to hook_out */
                        hook_out.Value = cur;
                        break;
                    }
                    if (this[p.points[0].X, p.points[0].Y - 1])
                    {
                        p.InsertBeforeHook(hook_in);
                    }
                    else
                    {
                        p.InsertBeforeHook(hook_out);
                    }
                }

                /* clear bm */
                ClearWithBbox(bbox);

                /* now schedule head->childlist and head->next for further
                   processing */
                if (head.Next != null)
                {
                    head.Next.ChildList = heap;
                    heap = head.Next;

                }
                if (head.ChildList != null)
                {
                    head.ChildList.ChildList = heap;
                    heap = head.ChildList;
                }
            }

            /* copy sibling structure from "next" to "sibling" component */
            for (Path? p = plist, p1; p != null; p = p1)
            {
                ctoken.ThrowIfCancellationRequested();

                p1 = p.Sibling;
                p.Sibling = p.Next;
            }

            /* reconstruct a new linked list ("next") structure from tree
               ("childlist", "sibling") structure. This code is slightly messy,
               because we use a heap to make it tail recursive: the heap
               contains a list of childlists which still need to be
               processed. */
            heap = plist;
            if (heap != null)
            {
                heap.Next = null;     /* heap is a linked list of childlists */
            }
            plist = null;
            Hook plist_hook = new Hook(() => plist, path => plist = path);
            while (heap != null)
            {
                ctoken.ThrowIfCancellationRequested();

                Path? heap1 = heap.Next;
                for (Path? p = heap; p != null; p = p.Sibling)
                {
                    ctoken.ThrowIfCancellationRequested();

                    /* p is a positive path */
                    /* append to linked list */
                    p.InsertBeforeHook(plist_hook);

                    /* go through its children */
                    for (Path? p1 = p.ChildList; p1 != null; p1 = p1.Sibling)
                    {
                        ctoken.ThrowIfCancellationRequested();

                        /* append to linked list */
                        p1.InsertBeforeHook(plist_hook);
                        /* append its childlist to heap, if non-empty */
                        if (p1.ChildList != null)
                        {
                            var hook = new Hook(() => heap1, path => heap1 = path);
                            while (hook.Value != null)
                            {
                                Path path = hook.Value;
                                hook.Set(() => path.Next, q => path.Next = q);
                            }
                            p1.ChildList.Next = hook.Value;
                            hook.Value = p1.ChildList;
                        }
                    }
                }
                heap = heap1;
            }
        }

        /* set the excess padding to 0 */
        private void ClearExcess()
        {
            if (Width % BM_WORDBITS != 0)
            {
                ulong mask = BM_ALLBITS << (BM_WORDBITS - (Width % BM_WORDBITS));
                for (int y = 0; y < Height; y++)
                {
                    *GetIndex(Width, y) &= mask;
                }
            }
        }

        private void Clear()
        {
            ulong* bptr = GetBase();
            for (int i = 0; i < GetSize(dy, Height); i++)
            {
                *(bptr + i) = 0UL;
            }
        }

        /* clear the bm, assuming the bounding box is set correctly (faster
           than clearing the whole bitmap) */
        private void ClearWithBbox(in Bbox bbox)
        {
            int imin = bbox.X0 / BM_WORDBITS;
            int imax = (bbox.X1 + BM_WORDBITS - 1) / BM_WORDBITS;
            int i, y;
            for (y = bbox.Y0; y < bbox.Y1; y++)
            {
                for (i = imin; i < imax; i++)
                {
                    GetScanLine(y)[i] = 0;
                }
            }
        }

        /* compute a path in the given pixmap, separating black from white.
           Start path at the point (x0,x1), which must be an upper left corner
           of the path. Also compute the area enclosed by the path. Return a
           new path_t object, or NULL on error (note that a legitimate path
           cannot have length 0). Sign is required for correct interpretation
           of turnpolicies. */
        private Path FindPath(int x0, int y0, bool sign, TurnPolicy turnpolicy, CancellationToken cancellationToken = default)
        {
            int x = x0;
            int y = y0;
            int dirx = 0;
            int diry = -1;
            List<IntPoint> pt = new List<IntPoint>();
            long area = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                /* add point to path */
                pt.Add(new IntPoint(x, y));

                /* move to next point */
                x += dirx;
                y += diry;
                area += x * diry;

                /* path complete? */
                if (x == x0 && y == y0)
                {
                    break;
                }

                /* determine next direction */
                int k = (dirx + diry - 1) / 2;

                int cx = x + k;
                int cy = y + (diry - dirx - 1) / 2;
                bool c = this[cx, cy];

                int dx = x + (dirx - diry - 1) / 2;
                int dy = y + k;
                bool d = this[dx, dy];

                if (c && !d)
                {
                    switch (turnpolicy)
                    {
                        case TurnPolicy.Right:
                        case TurnPolicy.Black when sign:
                        case TurnPolicy.White when !sign:
                        case TurnPolicy.Random when Convert.ToBoolean(rng!.Next(0, 1)):
                        case TurnPolicy.Majority when Majority(x, y):
                        case TurnPolicy.Minority when !Majority(x, y):
                            /* right turn */
                            RightTurn(ref dirx, ref diry);
                            break;
                        default:
                            /* left turn */
                            LeftTurn(ref dirx, ref diry);
                            break;
                    }
                }
                else if (c)
                {
                    /* right turn */
                    RightTurn(ref dirx, ref diry);
                }
                else if (!d)
                {
                    /* left turn */
                    LeftTurn(ref dirx, ref diry);
                }
            } /* while this path */
            return new Path(area, sign, pt.ToArray());
        }

        /* return the "majority" value of bitmap bm at intersection (x,y). We
           assume that the bitmap is balanced at "radius" 1.  */
        private bool Majority(int x, int y)
        {
            for (var i = 2; i < 5; i++) /* check at "radius" i */
            {
                int ct = 0;
                for (int a = -i + 1; a <= i - 1; a++)
                {
                    ct += BM_IGET(x + a, y + i - 1);
                    ct += BM_IGET(x + i - 1, y + a - 1);
                    ct += BM_IGET(x + a - 1, y - i);
                    ct += BM_IGET(x - i, y + a);
                }
                if (ct != 0)
                {
                    return ct > 0;
                }
            }
            return false;
        }

        /* find the next set pixel in a row <= y. Pixels are searched first
           left-to-right, then top-down. In other words, (x,y)<(x',y') if y>y'
           or y=y' and x<x'. If found, return true and store pixel in
           (*xp,*yp). Else return false. Note that this function assumes that
           excess bytes have been cleared with bm_clearexcess. */
        private bool FindNext(ref int xp, ref int yp)
        {
            int x0 = xp & ~(BM_WORDBITS - 1);

            for (int y = yp; y >= 0; y--)
            {
                for (int x = x0; x < Width && x >= 0; x += BM_WORDBITS)
                {
                    if (*GetIndex(x, y) != 0)
                    {
                        while (!BM_GET(x, y))
                        {
                            x++;
                        }
                        /* found */
                        xp = x;
                        yp = y;
                        return true;
                    }
                }
                x0 = 0;
            }
            /* not found */
            return false;
        }

        /* a path is represented as an array of points, which are thought to
           lie on the corners of pixels (not on their centers). The path point
           (x,y) is the lower left corner of the pixel (x,y). Paths are
           represented by the len/pt components of a path_t object (which
           also stores other information about the path) */

        /* xor the given pixmap with the interior of the given path. Note: the
           path must be within the dimensions of the pixmap. */
        private void XorPath(Path path)
        {
            int n = path.points.Length;
            if (n <= 0) /* a path of length 0 is silly, but legal */
            {
                return;
            }

            int y1 = path.points[n - 1].Y;
            int xa = path.Point0.X & -BM_WORDBITS;

            foreach (IntPoint point in path.points)
            {
                int x = point.X;
                int y = point.Y;
                if (y != y1)
                {
                    /* efficiently invert the rectangle [x,xa] x [y,y1] */
                    XorToRef(x, Math.Min(y, y1), xa);
                    y1 = y;
                }
            }
        }

        /* efficiently invert bits [x,infty) and [xa,infty) in line y. Here xa
           must be a multiple of BM_WORDBITS. */
        private void XorToRef(int x, int y, int xa)
        {
            int xhi = x & -BM_WORDBITS;
            int xlo = x & (BM_WORDBITS - 1);  /* = x % BM_WORDBITS */
            int i;

            if (xhi < xa)
            {
                for (i = xhi; i < xa; i += BM_WORDBITS)
                {
                    *GetIndex(i, y) ^= BM_ALLBITS;
                }
            }
            else
            {
                for (i = xa; i < xhi; i += BM_WORDBITS)
                {
                    *GetIndex(i, y) ^= BM_ALLBITS;
                }
            }

            /* note: the following "if" is needed because x86 treats a<<b as
               a<<(b&31). I spent hours looking for this bug. */
            if (xlo != 0)
            {
                *GetIndex(xhi, y) ^= BM_ALLBITS << (BM_WORDBITS - xlo);
            }
        }

        #region Unsafe

        /* calculate the base address of the bitmap data. Assume that the
           bitmap is well-formed, i.e., its size fits into the ptrdiff_t type.
           This is the case if created with bm_new or bm_dup. The base address
           may differ from bm->map if dy is negative */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong* GetBase()
        {
            return dy >= 0 || Height == 0 ? ptr : GetScanLine(Height - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong* GetScanLine(int y)
        {
            return ptr + y * dy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong* GetIndex(int x, int y)
        {
            return GetScanLine(y) + x / BM_WORDBITS;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool BM_UGET(int x, int y)
        {
            return (*GetIndex(x, y) & Mask(x)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong BM_USET(int x, int y)
        {
            return *GetIndex(x, y) |= Mask(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong BM_UCLR(int x, int y)
        {
            return *GetIndex(x, y) &= ~Mask(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong BM_UINV(int x, int y)
        {
            return *GetIndex(x, y) ^= Mask(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong BM_UPUT(int x, int y, bool b)
        {
            return b ? BM_USET(x, y) : BM_UCLR(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsBlackUnsafe(int x, int y)
        {
            return BM_UGET(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetBlackUnsafe(int x, int y)
        {
            BM_USET(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetWhiteUnsafe(int x, int y)
        {
            BM_UCLR(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InverseColorUnsafe(int x, int y)
        {
            BM_UINV(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetColorUnsafe(int x, int y, bool isBlack)
        {
            BM_UPUT(x, y, isBlack);
        }

        #endregion

        #region Safe

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Check(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool BM_GET(int x, int y)
        {
            return Check(x, y) && BM_UGET(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BM_IGET(int x, int y)
        {
            return BM_GET(x, y) ? 1 : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong BM_SET(int x, int y)
        {
            return Check(x, y) ? BM_USET(x, y) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong BM_CLR(int x, int y)
        {
            return Check(x, y) ? BM_UCLR(x, y) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong BM_INV(int x, int y)
        {
            return Check(x, y) ? BM_UINV(x, y) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong BM_PUT(int x, int y, bool b)
        {
            return Check(x, y) ? BM_UPUT(x, y, b) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsBlack(int x, int y)
        {
            return BM_GET(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetBlack(int x, int y)
        {
            BM_SET(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetWhite(int x, int y)
        {
            BM_CLR(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InverseColor(int x, int y)
        {
            BM_INV(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetColor(int x, int y, bool isBlack)
        {
            BM_PUT(x, y, isBlack);
        }

        #endregion

        #endregion

        #region Private Static Methods

        /* Find the bounding box of a given path. Path is assumed to be of
           non-zero length. */
        private static Bbox SetBboxPath(Path p)
        {
            int x0, y0;
            int x1, y1;

            x0 = y0 = int.MaxValue;
            x1 = y1 = 0;

            foreach (IntPoint point in p.points)
            {
                (int x, int y) = point;
                (x0, y0) = (Math.Min(x0, x), Math.Min(y0, y));
                (x1, y1) = (Math.Max(x1, x), Math.Max(y1, y));
            }
            return new Bbox(x0, x1, y0, y1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RightTurn(ref int x, ref int y)
        {
            (x, y) = (y, -x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LeftTurn(ref int x, ref int y)
        {
            (x, y) = (-y, x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetDy(int width)
        {
            return width == 0 ? 0 : (width - 1) / BM_WORDBITS + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetSize(int dy, int h)
        {
            dy = Math.Abs(dy);
            return checked(dy * h * BM_WORDSIZE);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mask(int x)
        {
            return BM_HIBIT >> ((x) & (BM_WORDBITS - 1));
        }

        #endregion

        #region Public Static Methods

        public static PotraceBitmap FromRgbx(IntPtr scan0, int width, int height, double c = .45)
        {
            Ensure.IsNotNull(scan0, nameof(scan0));
            Ensure.IsGreaterThan(width, 0, nameof(width));
            Ensure.IsGreaterThan(height, 0, nameof(height));
            Ensure.IsInRange(c, 0.0, 1.0, nameof(c));

            int stride = width * 4;
            c = c * 255 * 3;
            PotraceBitmap bm = new PotraceBitmap(width, height);
            try
            {
                unsafe
                {
                    byte* src = (byte*)(void*)scan0;
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = src + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int t = 0;
                            for (int i = 0; i < 3; i++) // we're ignoring the alpha channel
                            {
                                t += *(row++);
                            }
                            row++;
                            if (t < c)
                            {
                                bm.SetBlackUnsafe(x, height - y - 1);
                            }
                        }
                    }
                }
                return bm;
            }
            catch (Exception)
            {
                bm.Dispose();
                throw;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!disposed)
            {
                handle.Free();
                *ptr = 0;
                disposed = true;
            }
        }

        #endregion

        #region ICloneable

        public unsafe object Clone()
        {
            PotraceBitmap bm = new PotraceBitmap(Width, Height);
            try
            {
                Array.Copy(buffer, bm.buffer, buffer.Length);
            }
            catch
            {
                bm.Dispose();
                throw;
            }
            return bm;
        }

        #endregion

        #region Bbox

        private readonly struct Bbox
        {
            public readonly int X0;
            public readonly int X1;
            public readonly int Y0;
            public readonly int Y1;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Bbox(int x0, int x1, int y0, int y1)
            {
                X0 = x0;
                X1 = x1;
                Y0 = y0;
                Y1 = y1;
            }
        }

        #endregion
    }
}
