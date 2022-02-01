// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Xml;

namespace BitmapVectorizer
{
    public class SvgBackEnd : BackEnd
    {
        #region Fields

        private char lastop;
        private int pid = 1;
        private VECTOR cur;
        protected static readonly XmlWriterSettings DefaultXmlWriterSettings = new() { Indent = true };
        protected XmlWriter? xml;

        #endregion

        #region Properties

        public override string Name => "svg";

        public override string Extension => ".svg";

        public sealed override bool MultiPage => false;

        public sealed override bool OptiCurve => false;

        public sealed override BackEndType Type => BackEndType.DimensionBased;

        public sealed override FLOAT Rx { get; set; } = 96;

        public sealed override FLOAT Ry { get; set; } = 96;

        public string? Metadata { get; set; }

        public FLOAT Unit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        } = 10;

        public int ColumnWidth
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        } = 75;

        public bool UseDecimal
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        }

        public bool UseAbsoluteCommands
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        }

        public virtual bool UseGroups
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        }

        public virtual bool Opaque { get; set; }

        public bool WriteEachPath { get; set; }

        public bool Jaggy { get; set; }

        public int Color { get; set; }

        public int FillColor { get; set; } = 0xffffff;

        #endregion

        #region Public

        public override void Save(Stream output, TraceResult trace, ImageInfo imginfo, CancellationToken cancellationToken = default)
        {
            Ensure.IsNotNull(output, nameof(output));
            Ensure.IsNotNull(trace, nameof(trace));
            Ensure.IsNotNull(imginfo, nameof(imginfo));
            if (trace.FirstPath is null) { throw new ArgumentException("trace result does not contain any path.", nameof(trace)); }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (StreamWriter fout = new StreamWriter(output, Encoding.UTF8, 8192, leaveOpen: true))
                using (xml = XmlWriter.Create(fout, DefaultXmlWriterSettings))
                {
                    CalcDimensions(imginfo, trace.FirstPath);
                    WriteTopPart(imginfo);
                    if (Opaque)
                    {
                        WritePathsOpaque(trace.FirstPath, cancellationToken);
                    }
                    else
                    {
                        WritePathsTransparent(trace.FirstPath, cancellationToken);
                    }
                    WriteBottomPart();
                    xml.Flush();
                }
            }
            finally
            {
                pid = 1;
            }
        }

        #endregion

        #region Private Methods

        private void WriteTopPart(ImageInfo imginfo)
        {
            if (xml is null) { throw new InvalidOperationException(); }

            Trans trans = imginfo.Trans;
            FLOAT bboxx = trans.Bb[0] + imginfo.Lmar + imginfo.Rmar;
            FLOAT bboxy = trans.Bb[1] + imginfo.Tmar + imginfo.Bmar;
            FLOAT origx = trans.Orig[0] + imginfo.Lmar;
            FLOAT origy = bboxy - trans.Orig[1] - imginfo.Bmar;
            FLOAT scalex = trans.ScaleX / Unit;
            FLOAT scaley = trans.ScaleY / Unit;

            /* header */
            /* set bounding box and namespace */
            xml.WriteStartDocument();
            xml.WriteDocType("svg", "-//W3C//DTD SVG 20010904//EN", "http://www.w3.org/TR/2001/REC-SVG-20010904/DTD/svg10.dtd", null);
            xml.WriteStartElement("svg", "http://www.w3.org/2000/svg");
            xml.WriteAttributeString("version", "1.0");
            string bboxxs = FMT(bboxx);
            string bboxys = FMT(bboxy);
            xml.WriteAttributeString("width", $"{bboxxs}pt");
            xml.WriteAttributeString("height", $"{bboxys}pt");
            xml.WriteAttributeString("viewBox", $"0 0 {bboxxs} {bboxys}");
            xml.WriteAttributeString("preserveAspectRatio", "xMidYMid meet");

            /* metadata: creator */
            if (!String.IsNullOrEmpty(Metadata))
            {
                xml.WriteStartElement("metadata");
                xml.WriteString(Metadata);
                xml.WriteEndElement();
            }

            /* use a "group" tag to establish coordinate system and style */
            WriteStartGroup();
            xml.WriteStartAttribute("transform");

            if (origx != 0 || origy != 0)
            {
                xml.WriteString($"translate({FMT(origx)},{FMT(origy)})");
            }

            if (imginfo.Angle > 0)
            {
                xml.WriteString($"rotate({FMT(-imginfo.Angle)})");
            }

            if (scalex != 1 && scaley != 1)
            {
                xml.WriteString($"scale({FMT(scalex, precision: 7)},{FMT(scaley, precision: 7)})");
            }
            xml.WriteEndAttribute();
            WriteFillAttribute(Color);
        }

        private void WriteFillAttribute(int argb)
        {
            if (xml is null) { throw new InvalidOperationException(); }
            if ((argb & 0xffffff) == 0) { return; }
            xml.WriteAttributeString("fill", ToOpaqueHtmlColor(argb));
        }

        private void WriteBottomPart()
        {
            if (xml is null) { throw new InvalidOperationException(); }
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VECTOR UnitFunc(VECTOR p)
        {
            p *= Unit;
            FLOAT x, y;
            if (UseDecimal)
            {
                (x, y) = (p.X, -p.Y);
            }
            else
            {
                (x, y) = ((FLOAT)Math.Round(p.X), (FLOAT)(-Math.Round(p.Y)));
            }
            return new VECTOR(x, y);
        }

        private void ShipToken(in VECTOR p)
        {
            if (xml is null) { throw new InvalidOperationException(); }
            xml.WriteString(FMT(p.X));
            xml.WriteString(" ");
            xml.WriteString(FMT(p.Y));
            xml.WriteString(" ");
        }

        private void ShipToken(char token)
        {
            if (xml is null) { throw new InvalidOperationException(); }
            xml.WriteString(token.ToString());
            xml.WriteString(" ");
        }

        private void ShipCommand(char op, params VECTOR[] points)
        {
            if (points.Length == 0) { return; }

            bool rel = char.IsLower(op);
            if (lastop != op)
            {
                ShipToken(op);
            }

            int i = 0;
            VECTOR p;
            for (int length = points.Length - 1; i < length; i++)
            {
                p = UnitFunc(points[i]);
                if (rel)
                {
                    p -= cur;
                }
                ShipToken(p);
            }

            p = UnitFunc(points[i]);
            if (rel)
            {
                (p, cur) = (p - cur, p);
            }
            else
            {
                cur = p;
            }

            ShipToken(p);
            lastop = op;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveTo(in VECTOR p) => ShipCommand('M', p);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RMoveTo(in VECTOR p) => ShipCommand('m', p);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LineTo(in VECTOR p) => ShipCommand('L', p);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RLineTo(in VECTOR p) => ShipCommand('l', p);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CurveTo(in VECTOR p1, in VECTOR p2, in VECTOR p3) => ShipCommand('C', p1, p2, p3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RCurveTo(in VECTOR p1, in VECTOR p2, in VECTOR p3) => ShipCommand('c', p1, p2, p3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClosePath() => ShipToken(UseAbsoluteCommands ? 'Z' : 'z');

        private void WriteJaggyPath(IntPoint[] pt, bool abs = false)
        {
            int i;
            IntPoint cur, prev;
            int n = pt.Length;
            if (abs || UseAbsoluteCommands)
            {
                cur = prev = pt[n - 1];
                MoveTo((VECTOR)cur);
                for (i = 0; i < n; i++)
                {
                    if (pt[i] != cur)
                    {
                        cur = prev;
                        LineTo((VECTOR)cur);
                    }
                    prev = pt[i];
                }
                LineTo((VECTOR)pt[n - 1]);
            }
            else
            {
                cur = prev = pt[0];
                RMoveTo((VECTOR)cur);
                for (i = n - 1; i >= 0; i--)
                {
                    if (pt[i] != cur)
                    {
                        cur = prev;
                        LineTo((VECTOR)cur);
                    }
                    prev = pt[i];
                }
                RLineTo((VECTOR)pt[0]);
            }
            ClosePath();
        }

        private void WritePath(PrivCurve? pcurve, bool abs = false)
        {
            if (pcurve is null) { return; }

            if (abs || UseAbsoluteCommands)
            {
                MoveTo(pcurve.Point0);
            }
            else
            {
                RMoveTo(pcurve.Point0);
            }

            foreach (Segment segment in pcurve)
            {
                if (segment.Type == SegmentType.Corner)
                {
                    if (UseAbsoluteCommands)
                    {
                        LineTo(segment.C1);
                        LineTo(segment.EndPoint);
                    }
                    else
                    {
                        RLineTo(segment.C1);
                        RLineTo(segment.EndPoint);
                    }
                }
                else
                {
                    if (UseAbsoluteCommands)
                    {
                        CurveTo(segment.C0, segment.C1, segment.EndPoint);
                    }
                    else
                    {
                        RCurveTo(segment.C0, segment.C1, segment.EndPoint);
                    }
                }
            }
            ClosePath();
        }

        internal void WritePath(Path path, bool abs = false)
        {
            if (Jaggy)
            {
                WriteJaggyPath(path.points, abs);
            }
            else
            {
                WritePath(path.FCurves, abs);
            }
        }

        private void WriteStartPath()
        {
            if (xml is null) { throw new InvalidOperationException(); }
            xml.WriteStartElement("path");
            xml.WriteAttributeString("id", $"path{pid++}");
            xml.WriteStartAttribute("d");
            lastop = ' ';
        }

        private void WriteEndPath()
        {
            if (xml is null) { throw new InvalidOperationException(); }
            xml.WriteEndElement();
        }

        private void WriteStartGroup()
        {
            if (xml is null) { throw new InvalidOperationException(); }
            xml.WriteStartElement("g");
        }

        private void WriteEndGroup()
        {
            if (xml is null) { throw new InvalidOperationException(); }
            xml.WriteEndElement();
        }

        #region Opaque

        private void WritePathsOpaque(Path? plist, CancellationToken cancellationToken = default)
        {
            plist?.ForEachSibling(p =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (UseGroups)
                {
                    WriteStartGroup();
                    WriteStartGroup();
                }

                WriteStartPath();
                WritePath(p, abs: true);
                WriteFillAttribute(Color);
                WriteEndPath();

                if (p.ChildList != null)
                {
                    bool abs = true;
                    if (!WriteEachPath)
                    {
                        WriteStartPath();
                    }

                    p.ChildList.ForEachSibling(q =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (WriteEachPath)
                        {
                            WriteStartPath();
                            abs = true;
                        }

                        WritePath(q, abs: abs);

                        if (WriteEachPath)
                        {
                            WriteFillAttribute(FillColor);
                            WriteEndPath();
                        }
                        else
                        {
                            abs = false;
                        }
                    });

                    if (!WriteEachPath)
                    {
                        WriteFillAttribute(FillColor);
                        WriteEndPath();
                    }
                }

                if (UseGroups)
                {
                    WriteEndGroup();
                }

                p.ChildList?.ForEachSibling(q => WritePathsOpaque(q.ChildList, cancellationToken));

                if (UseGroups)
                {
                    WriteEndGroup();
                }
            });
        }

        #endregion

        #region Transparent

        private void WritePathsTransparentRec(Path? plist, CancellationToken cancellationToken = default)
        {
            plist?.ForEachSibling(p =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (UseGroups)
                {
                    WriteStartGroup();
                    WriteStartPath();
                }
                WritePath(p, abs: true);
                p.ChildList?.ForEachSibling(q =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WritePath(q, abs: false);
                });

                if (UseGroups)
                {
                    WriteEndPath();
                }

                p.ChildList?.ForEachSibling(q => WritePathsTransparentRec(q.ChildList, cancellationToken));

                if (UseGroups)
                {
                    WriteEndGroup();
                }
            });
        }

        private void WritePathsTransparent(Path plist, CancellationToken cancellationToken = default)
        {
            if (!UseGroups)
            {
                WriteStartPath();
            }

            WritePathsTransparentRec(plist, cancellationToken);

            if (!UseGroups)
            {
                WriteEndPath();
            }
        }

        #endregion

        #endregion

        #region Static

        private static string ToOpaqueHtmlColor(int argb)
        {
            int r = argb >> 16 & 0xff;
            int g = argb >> 8 & 0xff;
            int b = argb & 0xff;
            return
                Math.DivRem(r, 16, out int r1) == r1 &&
                Math.DivRem(g, 16, out int g1) == g1 &&
                Math.DivRem(b, 16, out int b1) == b1
                ? $"#{r1:X1}{g1:X1}{b1:X1}"
                : $"#{r:X2}{g:X2}{b:X2}";
        }

        protected static string FMT(FLOAT d, int precision = 3)
        {
            StringBuilder sb = new StringBuilder("#.", precision + 2);
            sb.Append('#', precision);
            string s = sb.ToString();
            s = d.ToString(s, NumberFormatInfo.InvariantInfo);
            return s.Length > 0 ? s : "0";
        }

        #endregion
    }
}
