using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace FFXIVTataruHelper.EventArguments
{
    public class ColorListChangeEventArgs : TatruEventArgs
    {
        public List<Color> Colors { get; internal set; }

        public int ColorsId { get; internal set; }

        internal ColorListChangeEventArgs(Object sender) : base(sender) { }
    }
}
