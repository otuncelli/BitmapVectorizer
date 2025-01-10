// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Diagnostics.Contracts;

namespace BitmapVectorizer;

public static partial class Potrace
{
    private const FLOAT Cos179 = (FLOAT)(-0.999847695156391); // Math.Cos(179 * Math.PI / 180);

    /* it suffices that this is longer than any path; it need not be really infinite */
    private const int INFTY = 10_000_000;
    const FLOAT HALF = (FLOAT).5;

    #region Preparation

    /* ---------------------------------------------------------------------- */
    /* Preparation: fill in the sum* fields of a path (used for later
       rapid summing). */
    private static void CalcSums(Path pp)
    {
        int n = pp.points.Length;
        SumStruct[] sums = new SumStruct[n + 1];
        /* preparatory computation for later fast summing */
        for (int i = 0; i < n; i++)
        {
            IntPoint p = pp.points[i] - pp.Point0;
            sums[i + 1] = sums[i] + p;
        }
        pp.sums = sums;
    }

    #endregion

    #region Stage 1

    /* ---------------------------------------------------------------------- */
    /* Stage 1: determine the straight subpaths (Sec. 2.2.1). Fill in the
       "lon" component of a path object (based on pt/len).	For each i,
       lon[i] is the furthest index such that a straight line can be drawn
       from i to lon[i]. Return 1 on error with errno set, else 0. */

    /* this algorithm depends on the fact that the existence of straight
       subpaths is a triplewise property. I.e., there exists a straight
       line through squares i0,...,in iff there exists a straight line
       through i,j,k, for all i0<=i<j<k<=in. (Proof?) */

    /* this implementation of calc_lon is O(n^2). It replaces an older
       O(n^3) version. A "constraint" means that future points must
       satisfy xprod(constraint[0], cur) >= 0 and xprod(constraint[1],
       cur) <= 0. */

    /* Remark for Potrace 1.1: the current implementation of calc_lon is
       more complex than the implementation found in Potrace 1.0, but it
       is considerably faster. The introduction of the "nc" data structure
       means that we only have to test the constraints for "corner"
       points. On a typical input file, this speeds up the calc_lon
       function by a factor of 31.2, thereby decreasing its time share
       within the overall Potrace algorithm from 72.6% to 7.82%, and
       speeding up the overall algorithm by a factor of 3.36. On another
       input file, calc_lon was sped up by a factor of 6.7, decreasing its
       time share from 51.4% to 13.61%, and speeding up the overall
       algorithm by a factor of 1.78. In any case, the savings are
       substantial. */

    private static void CalcLon(Path pp)
    {
        int i, j;
        IntPoint[] constraint = new IntPoint[2];
        IntPoint[] pt = pp.points;
        int n = pt.Length;
        int[] ct = new int[4];
        int[] pivk = new int[n];    /* pivk[n] */
        int[] nc = new int[n];      /* nc[n]: next corner */
        int[] lon = new int[n];

        /* initialize the nc data structure. Point from each point to the
           furthest future point to which it is connected by a vertical or
           horizontal segment. We take advantage of the fact that there is
           always a direction change at 0 (due to the path decomposition
           algorithm). But even if this were not so, there is no harm, as
           in practice, correctness does not depend on the word "furthest"
           above.  */

        int k = 0;
        for (i = n - 1; i >= 0; i--)
        {
            if (pt[i].X != pt[k].X && pt[i].Y != pt[k].Y)
            {
                k = i + 1;  /* necessarily i<n-1 in this case */
            }
            nc[i] = k;
        }

        /* determine pivot points: for each i, let pivk[i] be the furthest k
           such that all j with i<j<k lie on a line connecting i,k. */
        for (i = n - 1; i >= 0; i--)
        {
            ct[3] = ct[2] = ct[1] = ct[0] = 0;

            /* keep track of "directions" that have occurred */
            IntPoint cur = pt[MathHelper.Mod(i + 1, n)] - pt[i];
            int dir = (3 + 3 * cur.X + cur.Y) / 2;
            ct[dir]++;

            constraint[1] = constraint[0] = IntPoint.Zero;

            /* find the next k such that no straight line from i to k */
            k = nc[i];
            int k1 = i;
            do
            {
                cur = pt[k] - pt[k1];
                dir = (3 + 3 * Math.Sign(cur.X) + Math.Sign(cur.Y)) / 2;
                ct[dir]++;

                /* if all four "directions" have occurred, cut this path */
                if (ct[3] != 0 && ct[2] != 0 && ct[1] != 0 && ct[0] != 0)
                {
                    pivk[i] = k1;
                    goto foundk;
                }

                cur = pt[k] - pt[i];

                /* see if current constraint is violated */
                if (VectorHelper.Cross(constraint[0], cur) < 0 || VectorHelper.Cross(constraint[1], cur) > 0)
                {
                    goto constraint_viol;
                }

                /* else, update constraint */
                if (Math.Abs(cur.X) > 1 || Math.Abs(cur.Y) > 1)
                {
                    bool xgtz = cur.X > 0;
                    bool ygtz = cur.Y > 0;
                    bool xgtez = xgtz || cur.X == 0;
                    bool ygtez = ygtz || cur.Y == 0;
                    // int x = cur.X + ((cur.Y >= 0 && (cur.Y > 0 || cur.X < 0)) ? 1 : -1);
                    int x = cur.X + ((ygtez && (ygtz || !xgtez)) ? 1 : -1);
                    // int y = cur.Y + ((cur.X <= 0 && (cur.X < 0 || cur.Y < 0)) ? 1 : -1);
                    int y = cur.Y + ((!xgtz && (!xgtez || !ygtez)) ? 1 : -1);
                    IntPoint off = new IntPoint(x, y);
                    if (VectorHelper.Cross(constraint[0], off) >= 0)
                    {
                        constraint[0] = off;
                    }
                    // x = cur.X + ((cur.Y <= 0 && (cur.Y < 0 || cur.X < 0)) ? 1 : -1);
                    x = cur.X + ((!ygtz && (!ygtez || !xgtez)) ? 1 : -1);
                    // y = cur.Y + ((cur.X >= 0 && (cur.X > 0 || cur.Y < 0)) ? 1 : -1);
                    y = cur.Y + ((xgtez && (xgtz || !ygtez)) ? 1 : -1);
                    off = new IntPoint(x, y);
                    if (VectorHelper.Cross(constraint[1], off) <= 0)
                    {
                        constraint[1] = off;
                    }
                }
                k1 = k;
                k = nc[k1];
            } while (MathHelper.Cyclic(k, i, k1));
constraint_viol:
            /* k1 was the last "corner" satisfying the current constraint, and
               k is the first one violating it. We now need to find the last
               point along k1..k which satisfied the constraint. */

            cur = pt[k] - pt[k1];
            IntPoint dk = new IntPoint(Math.Sign(cur.X), Math.Sign(cur.Y)); /* direction of k-k1 */
            cur = pt[k1] - pt[i];

            /* find largest integer j such that xprod(constraint[0], cur+j*dk)
               >= 0 and xprod(constraint[1], cur+j*dk) <= 0. Use bilinearity
               of xprod. */
            int a = VectorHelper.Cross(constraint[0], cur);
            int b = VectorHelper.Cross(constraint[0], dk);
            int c = VectorHelper.Cross(constraint[1], cur);
            int d = VectorHelper.Cross(constraint[1], dk);
            /* find largest integer j such that a+j*b>=0 and c+j*d<=0. This
               can be solved with integer arithmetic. */
            j = INFTY;
            if (b < 0)
            {
                j = MathHelper.FloorDiv(a, -b);
            }
            if (d > 0)
            {
                j = Math.Min(j, MathHelper.FloorDiv(-c, d));
            }
            pivk[i] = MathHelper.Mod(k1 + j, n);
foundk:
            ;
        } /* for i */

        /* clean up: for each i, let lon[i] be the largest k such that for
           all i' with i<=i'<k, i'<k<=pivk[i']. */

        j = pivk[n - 1];
        lon[n - 1] = j;

        for (i = n - 2; i >= 0; i--)
        {
            if (MathHelper.Cyclic(i + 1, pivk[i], j))
            {
                j = pivk[i];
            }
            lon[i] = j;
        }

        for (i = n - 1; MathHelper.Cyclic(MathHelper.Mod(i + 1, n), j, lon[i]); i--)
        {
            lon[i] = j;
        }

        pp.lon = lon;
    }

    #endregion

    #region Stage 2

    /* ---------------------------------------------------------------------- */
    /* Stage 2: calculate the optimal polygon (Sec. 2.2.2-2.2.4). */

    /* Auxiliary function: calculate the penalty of an edge from i to j in
       the given path. This needs the "lon" and "sum*" data. */

    private static double Penalty3(Path pp, int i, int j)
    {
        int n = pp.points.Length;

        /* assume 0<=i<j<=n  */
        Contract.Assume(0 <= i && i < j && j <= n);
        SumStruct[] sums = pp.sums!;
        IntPoint[] pt = pp.points;

        int r = 0; /* rotations from i to j */
        if (j >= n)
        {
            j -= n;
            r = 1;
        }

        SumStruct sum = sums[j + 1] - sums[i] + r * sums[n];
        FLOAT x, y, x2, xy, y2;
        (x, y, x2, xy, y2) = sum;
        double k = j + 1 - i + r * n;

        IntPoint p1 = pt[i] + pt[j];
        IntPoint p2 = pt[j] - pt[i];

        double px = p1.X / 2.0 - pt[0].X;
        double py = p1.Y / 2.0 - pt[0].Y;
        double ey = p2.X;
        double ex = -p2.Y;

        double a = (x2 - 2 * x * px) / k + px * px;
        double b = (xy - x * py - y * px) / k + px * py;
        double c = (y2 - 2 * y * py) / k + py * py;

        double s = ex * ex * a + 2 * ex * ey * b + ey * ey * c;

        return Math.Sqrt(s);
    }

    /* find the optimal polygon. Fill in the m and po components. Return 1
       on failure with errno set, else 0. Non-cyclic version: assumes i=0
       is in the polygon. Fixme: implement cyclic version. */
    private static void BestPolygon(Path pp)
    {
        int i, j, m, k;
        int n = pp.points.Length;
        double[] pen = new double[n + 1];   /* pen[n+1]: penalty vector */
        int[] prev = new int[n + 1];        /* prev[n+1]: best path pointer vector */
        int[] clip0 = new int[n];           /* clip0[n]: longest segment pointer, non-cyclic */
        int[] clip1 = new int[n + 1];       /* clip1[n+1]: backwards segment pointer, non-cyclic */
        int[] seg0 = new int[n + 1];        /* seg0[m+1]: forward segment bounds, m<=n */
        int[] seg1 = new int[n + 1];        /* seg1[m+1]: backward segment bounds, m<=n */

        double thispen;
        double best;
        int c;

        /* calculate clipped paths */
        for (i = 0; i < n; i++)
        {
            c = MathHelper.Mod(pp.lon![MathHelper.Mod(i - 1, n)] - 1, n);

            if (c == i)
            {
                c = MathHelper.Mod(i + 1, n);
            }
            clip0[i] = c < i ? n : c;
        }

        /* calculate backwards path clipping, non-cyclic. j <= clip0[i] iff
           clip1[j] <= i, for i,j=0..n. */
        j = 1;
        for (i = 0; i < n; i++)
        {
            while (j <= clip0[i])
            {
                clip1[j] = i;
                j++;
            }
        }

        /* calculate seg0[j] = longest path from 0 with j segments */
        for (i = 0, j = 0; i < n; j++)
        {
            seg0[j] = i;
            i = clip0[i];
        }
        seg0[j] = n;
        m = j;

        /* calculate seg1[j] = longest path to n with m-j segments */
        for (i = n, j = m; j > 0; j--)
        {
            seg1[j] = i;
            i = clip1[i];
        }

        /* now find the shortest path with m segments, based on penalty3 */
        /* note: the outer 2 loops jointly have at most n iterations, thus
           the worst-case behavior here is quadratic. In practice, it is
           close to linear since the inner loop tends to be short. */
        for (seg1[0] = 0, pen[0] = 0, j = 1; j <= m; j++)
        {
            for (i = seg1[j]; i <= seg0[j]; i++)
            {
                best = -1;
                for (k = seg0[j - 1]; k >= clip1[i]; k--)
                {
                    thispen = Penalty3(pp, k, i) + pen[k];
                    if (best < 0 || thispen < best)
                    {
                        prev[i] = k;
                        best = thispen;
                    }
                }
                pen[i] = best;
            }
        }

        /* read off shortest path */
        int[] po = new int[m];
        for (i = n, j = m - 1; i > 0; j--)
        {
            i = prev[i];
            po[j] = i;
        }
        pp.po = po;
    }

    #endregion

    #region Stage 3

    /* ---------------------------------------------------------------------- */
    /* Stage 3: vertex adjustment (Sec. 2.3.1). */

    /* Adjust vertices of optimal polygon: calculate the intersection of
       the two "optimal" line segments, then move it into the unit square
       if it lies outside. */
    private static void AdjustVertices(Path pp)
    {
        int m = pp.po!.Length;
        int[] po = pp.po;
        bool sign = pp.sign;
        IntPoint[] pt = pp.points;
        int n = pt.Length;
        IntPoint p0 = pp.Point0;
        VECTOR[] ctr = new VECTOR[m];   /* ctr[m] */
        VECTOR[] dir = new VECTOR[m];   /* dir[m] */

        FLOAT[,,] q = new FLOAT[m, 3, 3];   /* q[m] */
        FLOAT[] v = new FLOAT[3];
        FLOAT d;
        int i, j, k, l;
        PrivCurve pcurve = new PrivCurve(m);

        /* calculate "optimal" point-slope representation for each line
           segment */
        for (i = 0; i < m; i++)
        {
            j = po[MathHelper.Mod(i + 1, m)];
            j = MathHelper.Mod(j - po[i], n) + po[i];
            PointSlope(pp, po[i], j, ref ctr[i], ref dir[i]);
        }

        /* represent each line segment as a singular quadratic form; the
           distance of a point (x,y) from the line segment will be
           (x,y,1)Q(x,y,1)^t, where Q=q[i]. */
        for (i = 0; i < m; i++)
        {
            d = VectorHelper.Dot(dir[i], dir[i]);

            if (d == 0)
            {
                for (j = 0; j < 3; j++)
                {
                    for (k = 0; k < 3; k++)
                    {
                        q[i, j, k] = 0;
                    }
                }
            }
            else
            {
                v[0] = dir[i].Y;
                v[1] = -dir[i].X;
                v[2] = -v[1] * ctr[i].Y - v[0] * ctr[i].X;
                for (l = 0; l < 3; l++)
                {
                    for (k = 0; k < 3; k++)
                    {
                        q[i, l, k] = v[l] * v[k] / d;
                    }
                }
            }
        }

        /* now calculate the "intersections" of consecutive segments.
           Instead of using the actual intersection, we find the point
           within a given unit square which minimizes the square distance to
           the two lines. */

        /* the type of (affine) quadratic forms, represented as symmetric 3x3
           matrices.  The value of the quadratic form at a vector (x,y) is v^t
           Q v, where v = (x,y,1)^t. */
        FLOAT[,] Q = new FLOAT[3, 3];
        for (i = 0; i < m; i++)
        {
            VECTOR w;
            FLOAT dx, dy;
            FLOAT det;
            FLOAT min, cand;   /* minimum and candidate for minimum of quad. form */
            FLOAT xmin, ymin;  /* coordinates of minimum */
            int z;

            /* let s be the vertex, in coordinates relative to x0/y0 */
            VECTOR s = (VECTOR)(pt[po[i]] - p0);

            /* intersect segments i-1 and i */
            j = MathHelper.Mod(i - 1, m);

            /* add quadratic forms */
            for (l = 0; l < 3; l++)
            {
                for (k = 0; k < 3; k++)
                {
                    Q[l, k] = q[j, l, k] + q[i, l, k];
                }
            }
            while (true)
            {
                /* minimize the quadratic form Q on the unit square */
                /* find intersection */
                det = Q[0, 0] * Q[1, 1] - Q[0, 1] * Q[1, 0];
                if (det != 0)
                {
                    FLOAT wx = (-Q[0, 2] * Q[1, 1] + Q[1, 2] * Q[0, 1]) / det;
                    FLOAT wy = (Q[0, 2] * Q[1, 0] - Q[1, 2] * Q[0, 0]) / det;
                    w = new VECTOR(wx, wy);
                    break;
                }

                /* matrix is singular - lines are parallel. Add another,
                   orthogonal axis, through the center of the unit square */
                if (Q[0, 0] > Q[1, 1])
                {
                    v[0] = -Q[0, 1];
                    v[1] = Q[0, 0];
                }
                else if (Q[1, 1] != 0)
                {
                    v[0] = -Q[1, 1];
                    v[1] = Q[1, 0];
                }
                else
                {
                    v[0] = 1;
                    v[1] = 0;
                }
                d = v[0] * v[0] + v[1] * v[1];
                v[2] = -v[1] * s.Y - v[0] * s.X;
                for (l = 0; l < 3; l++)
                {
                    for (k = 0; k < 3; k++)
                    {
                        Q[l, k] += v[l] * v[k] / d;
                    }
                }
            }

            VECTOR dv = VectorHelper.Abs(w - s);
            int index;
            if (dv.X <= HALF && dv.Y <= HALF)
            {
                index = sign ? i : m - i - 1;
                w += (VECTOR)p0;
                pcurve.SetVertex(index, w);
                continue;
            }

            /* the minimum was not in the unit square; now minimize quadratic
               on boundary of square */
            min = QuadForm(Q, s);
            xmin = s.X;
            ymin = s.Y;

            if (Q[0, 0] != 0)
            {
                for (z = 0; z < 2; z++) /* value of the y-coordinate */
                {
                    FLOAT wY = s.Y - HALF + z;
                    FLOAT wX = -(Q[0, 1] * wY + Q[0, 2]) / Q[0, 0];
                    w = new VECTOR(wX, wY);
                    dx = Math.Abs(wX - s.X);
                    cand = QuadForm(Q, w);
                    if (dx <= HALF && cand < min)
                    {
                        min = cand;
                        xmin = w.X;
                        ymin = w.Y;
                    }
                }
            }

            if (Q[1, 1] != 0)
            {
                for (z = 0; z < 2; z++) /* value of the x-coordinate */
                {
                    FLOAT wX = s.X - HALF + z;
                    FLOAT wY = -(Q[1, 0] * wX + Q[1, 2]) / Q[1, 1];
                    w = new VECTOR(wX, wY);
                    dy = Math.Abs(wY - s.Y);
                    cand = QuadForm(Q, w);
                    if (dy <= HALF && cand < min)
                    {
                        min = cand;
                        xmin = w.X;
                        ymin = w.Y;
                    }
                }
            }

            /* check four corners */
            for (l = 0; l < 2; l++)
            {
                for (k = 0; k < 2; k++)
                {
                    w = new VECTOR(s.X - HALF + l, s.Y - HALF + k);
                    cand = QuadForm(Q, w);
                    if (cand < min)
                    {
                        min = cand;
                        xmin = w.X;
                        ymin = w.Y;
                    }
                }
            }
            index = sign ? i : m - i - 1;
            w = new VECTOR(xmin + p0.X, ymin + p0.Y);
            pcurve.SetVertex(index, w);
        }

        pp.Curves = pcurve;
    }

    /* Apply quadratic form Q to vector w = (w.x,w.y) */
    private static FLOAT QuadForm(FLOAT[,] Q, in VECTOR w)
    {
        FLOAT[] v = [w.X, w.Y, 1];
        FLOAT sum = 0;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                sum += v[i] * Q[i, j] * v[j];
            }
        }
        return sum;
    }

    /* determine the center and slope of the line i..j. Assume i<j. Needs
       "sum" components of p to be set. */
    private static void PointSlope(Path path, int i, int j, ref VECTOR ctr, ref VECTOR dir)
    {
        /* assume i<j */
        Contract.Assume(i < j);

        int n = path.points.Length;
        SumStruct[] sums = path.sums!;

        int r = 0;  /* rotations from i to j */

        while (j >= n)
        {
            j -= n;
            r += 1;
        }
        while (i >= n)
        {
            i -= n;
            r -= 1;
        }
        while (j < 0)
        {
            j += n;
            r -= 1;
        }
        while (i < 0)
        {
            i += n;
            r += 1;
        }

        SumStruct sum = sums[j + 1] - sums[i] + r * sums[n];
        FLOAT x, y, x2, xy, y2;
        (x, y, x2, xy, y2) = sum;
        FLOAT k = j + 1 - i + r * n;

        ctr = new VECTOR(x / k, y / k);

        FLOAT a = (x2 - x * x / k) / k;
        FLOAT b = (xy - x * y / k) / k;
        FLOAT c = (y2 - y * y / k) / k;

        FLOAT lambda2 = (a + c + MathHelper.Sqrt((a - c) * (a - c) + 4 * b * b)) / 2;    /* larger e.value */

        /* now find e.vector for lambda2 */
        a -= lambda2;
        c -= lambda2;
        FLOAT l;
        if (Math.Abs(a) >= Math.Abs(c))
        {
            l = MathHelper.Sqrt(a * a + b * b);
            if (l != 0)
            {
                dir = new VECTOR(-b / l, a / l);
            }
        }
        else
        {
            l = MathHelper.Sqrt(c * c + b * b);
            if (l != 0)
            {
                dir = new VECTOR(-c / l, b / l);
            }
        }
        if (l == 0)
        {
            /* sometimes this can happen when k=4:
               the two eigenvalues coincide */
            dir = default;
        }
    }

    #endregion

    #region Stage 4

    /* ---------------------------------------------------------------------- */
    /* Stage 4: smoothing and corner analysis (Sec. 2.3.3) */

    private static void Smooth(Path pp, FLOAT alphamax)
    {
        PrivCurve pcurve = pp.Curves!;
        int m = pcurve.Count;

        /* examine each vertex and find its best fit */
        for (int i = 0; i < m; i++)
        {
            int j = MathHelper.Mod(i + 1, m);
            int k = MathHelper.Mod(i + 2, m);
            VECTOR ivert = pcurve.GetVertex(i);
            VECTOR jvert = pcurve.GetVertex(j);
            VECTOR kvert = pcurve.GetVertex(k);
            VECTOR p4 = VectorHelper.Interval(HALF, kvert, jvert);

            FLOAT denom = VectorHelper.Ddenom(ivert, kvert);
            FLOAT alpha;

            if (denom != 0)
            {
                FLOAT dd = VectorHelper.Cross(ivert, jvert, kvert) / denom;
                dd = Math.Abs(dd);
                alpha = dd > 1 ? (1 - 1 / dd) : 0;
                alpha /= (FLOAT).75;
            }
            else
            {
                alpha = 4 / (FLOAT)3;
            }

            FLOAT alpha0 = alpha;  /* remember "original" value of alpha */

            if (alpha >= alphamax)  /* pointed corner */
            {
                pcurve[j] = new Segment(jvert, p4);
            }
            else
            {
                alpha = MathHelper.Clamp(alpha, (FLOAT).55, 1);
                VECTOR p2 = VectorHelper.Interval(HALF + HALF * alpha, ivert, jvert);
                VECTOR p3 = VectorHelper.Interval(HALF + HALF * alpha, kvert, jvert);
                pcurve[j] = new Segment(p2, p3, p4);
            }
            pcurve.SetAlpha0(j, alpha0);
            pcurve.SetAlpha(j, alpha);  /* store the "cropped" value of alpha */
            pcurve.SetBeta(j, HALF);
        }
    }

    #endregion

    #region Stage 5

    /* ---------------------------------------------------------------------- */
    /* Stage 5: Curve optimization (Sec. 2.4) */

    /* calculate best fit from i+.5 to j+.5.  Assume i<j (cyclically).
       Return true and set badness and parameters (alpha, beta), if
       possible. Return false if impossible. */
    private static bool OptiPenalty(Path pp, int i, int j, out Opti res, FLOAT opttolerance, int[] convc, FLOAT[] areac)
    {
        /* check convexity, corner-freeness, and maximum bend < 179 degrees */
        PrivCurve pcurve = pp.Curves!;
        int m = pcurve.Count;
        res = default;

        if (i == j) /* sanity - a full loop can never be an opticurve */
        {
            return false;
        }

        int k;
        int i1 = MathHelper.Mod(i + 1, m);
        int k1 = i1;
        int conv = convc[k1];
        if (conv == 0)
        {
            return false;
        }

        VECTOR ivert = pcurve.GetVertex(i);
        VECTOR i1vert = pcurve.GetVertex(i1);

        FLOAT d = VectorHelper.Distance(ivert, i1vert);
        for (k = k1; k != j; k = k1)
        {
            k1 = MathHelper.Mod(k + 1, m);
            int k2 = MathHelper.Mod(k + 2, m);
            if (convc[k1] != conv)
            {
                return false;
            }

            VECTOR k1vert = pcurve.GetVertex(k1);
            VECTOR k2vert = pcurve.GetVertex(k2);

            if (Math.Sign(VectorHelper.Cross(ivert, i1vert, k1vert, k2vert)) != conv ||
                VectorHelper.Dot(ivert, i1vert, k1vert, k2vert) < d * VectorHelper.Distance(k1vert, k2vert) * Cos179)
            {
                return false;
            }
        }

        /* the curve we're working in: */
        int modjm = MathHelper.Mod(j, m);
        VECTOR p0 = pcurve.GetEndPoint(MathHelper.Mod(i, m));
        VECTOR p1 = i1vert;
        VECTOR p2 = pcurve.GetVertex(modjm);
        VECTOR p3 = pcurve.GetEndPoint(modjm);

        /* determine its area */
        FLOAT area = areac[j] - areac[i];
        area -= VectorHelper.Cross(pcurve.GetVertex(0), pcurve.GetEndPoint(i), pcurve.GetEndPoint(j)) / 2;
        if (i >= j)
        {
            area += areac[m];
        }

        /* find intersection o of p0p1 and p2p3. Let t,s such that o =
           interval(t,p0,p1) = interval(s,p3,p2). Let A be the area of the
           triangle (p0,o,p3). */

        FLOAT A1 = VectorHelper.Cross(p0, p1, p2);
        FLOAT A2 = VectorHelper.Cross(p0, p1, p3);
        FLOAT A3 = VectorHelper.Cross(p0, p2, p3);
        /* double A4 = PointD.Cross(p1, p2, p3); */
        FLOAT A4 = A1 + A3 - A2;

        if (A2 == A1)   /* this should never happen */
        {
            return false;
        }

        FLOAT t = A3 / (A3 - A4);
        FLOAT s = A2 / (A2 - A1);
        FLOAT A = A2 * t / 2;

        if (A == 0)   /* this should never happen */
        {
            return false;
        }

        FLOAT R = area / A;    /* relative area */
        FLOAT alpha = 2 - MathHelper.Sqrt(4 - R / (FLOAT).3);  /* overall alpha for p0-o-p3 curve */

        p1 = VectorHelper.Interval(t * alpha, p0, p1);
        p2 = VectorHelper.Interval(s * alpha, p3, p2);    /* the proposed curve is now (p0,p1,p2,p3) */

        FLOAT penalty = 0, d1, t1;
        VECTOR pt;

        /* calculate penalty */
        /* check tangency with edges */
        for (k = MathHelper.Mod(i + 1, m); k != j; k = k1)
        {
            k1 = MathHelper.Mod(k + 1, m);
            VECTOR kvert = pcurve.GetVertex(k);
            VECTOR k1vert = pcurve.GetVertex(k1);
            t1 = VectorHelper.Tangent(p0, p1, p2, p3, kvert, k1vert);
            if (t1 < -HALF)
            {
                return false;
            }
            pt = VectorHelper.Bezier(t1, p0, p1, p2, p3);
            d = VectorHelper.Distance(kvert, k1vert);
            if (d == 0)   /* this should never happen */
            {
                return false;
            }
            d1 = VectorHelper.Cross(kvert, k1vert, pt) / d;
            if (Math.Abs(d1) > opttolerance ||
                VectorHelper.Dot(kvert, k1vert, pt) < 0 ||
                VectorHelper.Dot(k1vert, kvert, pt) < 0)
            {
                return false;
            }
            penalty += d1 * d1;
        }

        /* check corners */
        for (k = i; k != j; k = k1)
        {
            k1 = MathHelper.Mod(k + 1, m);
            VECTOR kep = pcurve.GetEndPoint(k);
            VECTOR k1ep = pcurve.GetEndPoint(k1);

            t1 = VectorHelper.Tangent(p0, p1, p2, p3, kep, k1ep);
            if (t1 < -HALF)
            {
                return false;
            }
            pt = VectorHelper.Bezier(t1, p0, p1, p2, p3);
            d = VectorHelper.Distance(kep, k1ep);
            if (d == 0)   /* this should never happen */
            {
                return false;
            }
            d1 = VectorHelper.Cross(kep, k1ep, pt) / d;
            FLOAT d2 = VectorHelper.Cross(kep, k1ep, pcurve.GetVertex(k1)) / d;
            d2 *= (FLOAT).75 * pcurve.GetAlpha(k1);
            if (d2 < 0)
            {
                d1 = -d1;
                d2 = -d2;
            }
            if (d1 < d2 - opttolerance)
            {
                return false;
            }
            if (d1 < d2)
            {
                d = d1 - d2;
                penalty += d * d;
            }
        }

        res = new Opti(penalty, p1, p2, t, s, alpha);
        return true;
    }

    /* optimize the path p, replacing sequences of Bezier segments by a
       single segment when possible. */
    private static void OptiCurve(Path pp, FLOAT opttolerance)
    {
        PrivCurve pcurve = pp.Curves!;
        int m = pcurve.Count;
        int[] pt = new int[m + 1];          /* pt[m+1] */
        FLOAT[] pen = new FLOAT[m + 1];   /* pen[m+1] */
        int[] len = new int[m + 1];         /* len[m+1] */
        Opti[] opt = new Opti[m + 1];       /* opt[m+1] */
        int[] convc = new int[m];           /* conv[m]: pre-computed convexities */
        FLOAT[] areac = new FLOAT[m + 1]; /* cumarea[m+1]: cache for fast area computation */
        VECTOR p0;
        int i, j, i1;

        /* pre-calculate convexity: +1 = right turn, -1 = left turn, 0 = corner */
        for (i = 0; i < m; i++)
        {
            if (pcurve[i].Type == SegmentType.Bezier)
            {
                p0 = pcurve.GetVertex(MathHelper.Mod(i - 1, m));
                VECTOR p1 = pcurve.GetVertex(i);
                VECTOR p2 = pcurve.GetVertex(MathHelper.Mod(i + 1, m));
                convc[i] = Math.Sign(VectorHelper.Cross(p0, p1, p2));
            }
            else
            {
                convc[i] = 0;
            }
        }

        /* pre-calculate areas */
        FLOAT area = 0;
        p0 = pcurve.GetVertex(0);
        for (i = 0; i < m; i++)
        {
            i1 = MathHelper.Mod(i + 1, m);
            if (pcurve[i1].Type == SegmentType.Bezier)
            {
                FLOAT alpha = pcurve.GetAlpha(i1);
                area += (FLOAT).3 * alpha * (4 - alpha) * VectorHelper.Cross(pcurve.GetEndPoint(i), pcurve.GetVertex(i1), pcurve[i1].EndPoint) / 2;
                area += VectorHelper.Cross(p0, pcurve.GetEndPoint(i), pcurve[i1].EndPoint) / 2;
            }
            areac[i + 1] = area;
        }

        pt[0] = -1;

        /* Fixme: we always start from a fixed point -- should find the best
           curve cyclically */
        for (j = 1; j <= m; j++)
        {
            /* calculate best path from 0 to j */
            pt[j] = j - 1;
            pen[j] = pen[j - 1];
            len[j] = len[j - 1] + 1;

            int modjm = MathHelper.Mod(j, m);
            for (i = j - 2; i >= 0; i--)
            {
                if (!OptiPenalty(pp, i, modjm, out Opti o, opttolerance, convc, areac))
                {
                    break;
                }
                if (len[j] > len[i] + 1 || (len[j] == len[i] + 1 && pen[j] > pen[i] + o.pen))
                {
                    opt[j] = o;
                    pt[j] = i;
                    pen[j] = pen[i] + o.pen;
                    len[j] = len[i] + 1;
                }
            }
        }

        int om = len[m];
        PrivCurve opcurve = new PrivCurve(om);

        FLOAT[] s = new FLOAT[om];
        FLOAT[] t = new FLOAT[om];

        j = m;
        for (i = om - 1; i >= 0; i--)
        {
            int modjm = MathHelper.Mod(j, m);
            if (pt[j] == j - 1)
            {
                opcurve[i] = pcurve[modjm];
                opcurve.SetVertex(i, pcurve.GetVertex(modjm));
                s[i] = t[i] = 1;
            }
            else
            {
                Opti o = opt[j];
                VECTOR endpoint = pcurve.GetEndPoint(modjm);
                opcurve[i] = new Segment(o.c0, o.c1, endpoint);
                opcurve.SetAlpha(i, o.alpha);
                opcurve.SetAlpha0(i, pcurve.GetAlpha0(modjm));
                opcurve.SetVertex(i, VectorHelper.Interval(o.s, endpoint, pcurve.GetVertex(modjm)));
                s[i] = o.s;
                t[i] = o.t;
            }
            j = pt[j];
        }

        /* calculate beta parameters */
        for (i = 0; i < om; i++)
        {
            i1 = MathHelper.Mod(i + 1, om);
            opcurve.SetBeta(i, s[i] / (s[i] + t[i1]));
        }

        pp.OptimizedCurves = opcurve;
    }

    #endregion
}
