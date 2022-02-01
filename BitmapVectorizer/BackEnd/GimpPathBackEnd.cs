// Copyright 2023 Osman Tunçelli. All rights reserved.
// Use of this source code is governed by a GPL license that can be found in the COPYING file.
// This file is a part of CSharp port of Potrace(R). "Potrace" is registered trademark of Peter Selinger.

using System;

namespace BitmapVectorizer
{
    public class GimpPathBackEnd : SvgBackEnd
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
    }
}
