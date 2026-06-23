using System;
using FFXIVTataruHelper.EventArguments;

namespace FFXIVTataruHelper.TataruComponentModel
{
    public class AsyncPropertyChangedEventArgs : TatruEventArgs
    {
        //
        // Summary:
        //     Gets the name of the property that changed.
        //
        // Returns:
        //     The name of the property that changed.
        public virtual string PropertyName { get; internal set; }

        internal AsyncPropertyChangedEventArgs(Object sender) : base(sender) { }

        internal AsyncPropertyChangedEventArgs(Object sender, string propertyName) : base(sender)
        {
            PropertyName = propertyName;
        }
    }
}
