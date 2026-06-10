using System;
using System.Windows.Media;

namespace FFXIVTataruHelper.EventArguments
{
    public class TranslationArrivedEventArgs : TatruEventArgs
    {
        public string Text { get; internal set; }

        public int ErrorCode { get; internal set; }

        internal TranslationArrivedEventArgs(Object sender) : base(sender) { }
    }
}
