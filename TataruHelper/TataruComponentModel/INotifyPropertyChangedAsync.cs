using FFXIVTataruHelper.EventArguments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFXIVTataruHelper.TataruComponentModel
{
    public interface INotifyPropertyChangedAsync
    {
        event AsyncEventHandler<AsyncPropertyChangedEventArgs> AsyncPropertyChanged;
    }
}
