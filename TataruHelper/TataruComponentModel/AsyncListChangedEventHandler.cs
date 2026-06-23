using System;
using System.ComponentModel;
using FFXIVTataruHelper.EventArguments;

namespace FFXIVTataruHelper.TataruComponentModel
{
    public class AsyncListChangedEventHandler<T> : TatruEventArgs
    {
        public virtual ListChangedEventArgs ChangedEventArgs { get; internal set; }

        public virtual T ChangedElemnt { get; internal set; }

        internal AsyncListChangedEventHandler(Object sender) : base(sender) { }

        internal AsyncListChangedEventHandler(Object sender, T changedElement, ListChangedEventArgs changedEventArgs) : base(sender)
        {
            ChangedEventArgs = changedEventArgs;
            ChangedElemnt = changedElement;
        }
    }
}
