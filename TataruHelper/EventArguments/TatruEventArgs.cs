using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class TatruEventArgs : System.EventArgs
    {
        public Object Sender { get; internal set; }

        public TatruEventArgs(Object sender)
        {
            Sender = sender;
        }
    }
}
