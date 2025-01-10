// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace BitmapVectorizer;

public sealed class PdnShapeBackEnd : SvgBackEnd
{
    public override bool Opaque
    {
        get => false;
        set => throw new NotSupportedException();
    }

    public override bool UseGroups
    {
        get => false;
        set => throw new NotSupportedException();
    }

    public string DisplayName { get; set; } = "Untitled";

    public override string Name => "pdnshape";

    public override string Extension => ".xaml";

    private static string FormatNamespace(string s) => $"clr-namespace:{s};assembly=PaintDotNet.Framework";

    private void WriteTopPart()
    {
        if (xml is null) { throw new InvalidOperationException(); }

        const string PdnUIMediaNS = "PaintDotNet.UI.Media";
        const string PdnShapesNS = "PaintDotNet.Shapes";
        xml.WriteStartDocument(standalone: false);
        xml.WriteStartElement("ps", "SimpleGeometryShape", FormatNamespace(PdnShapesNS));
        xml.WriteAttributeString("xmlns", "", null, FormatNamespace(PdnUIMediaNS));
        xml.WriteAttributeString("xmlns", "ps", null, FormatNamespace(PdnShapesNS));
        xml.WriteAttributeString("DisplayName", DisplayName);
        xml.WriteStartAttribute("Geometry");
    }

    private void WritePathsTransparent(Path? plist, CancellationToken cancellationToken = default)
    {
        plist?.ForEachSibling(p =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            WritePath(p, abs: true);
            if (p.ChildList != null)
            {
                p.ChildList.ForEachSibling(q =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WritePath(q, abs: false);
                });
                p.ChildList.ForEachSibling(q => WritePathsTransparent(q.ChildList, cancellationToken));
            }
        });
    }

    private void WriteBottomPart()
    {
        if (xml is null) { throw new InvalidOperationException(); }

        xml.WriteEndElement();
    }

    public override void Save(Stream output, TraceResult trace, ImageInfo imginfo, CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNull(output, nameof(output));
        Ensure.IsNotNull(trace, nameof(trace));
        Ensure.IsNotNull(imginfo, nameof(imginfo));

        cancellationToken.ThrowIfCancellationRequested();
        using (StreamWriter fout = new StreamWriter(output, Encoding.UTF8, 8192, leaveOpen: true))
        using (xml = XmlWriter.Create(fout, DefaultXmlWriterSettings))
        {
            WriteTopPart();
            WritePathsTransparent(trace.FirstPath, cancellationToken);
            WriteBottomPart();
        }
    }
}
