using System;
using System.Threading.Tasks;

namespace FFXIVTataruHelper
{
    static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            task.ContinueWith(
                t => { Logger.WriteLog(t.Exception); },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        public static void EndWith(this Task task, Action action)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.WriteLog(t.Exception);

                action();
            });
        }
    }
}
