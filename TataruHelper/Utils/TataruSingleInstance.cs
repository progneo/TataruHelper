using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using FFXIVTataruHelper.WinUtils;

namespace FFXIVTataruHelper.Utils
{
    static public class TataruSingleInstance
    {
        public static readonly int WM_SHOWFIRSTINSTANCE =
            Win32Interfaces.RegisterWindowMessageM("WM_SHOWFIRSTINSTANCE|{0}", ProgramInfo.AssemblyGuid);

        private static Mutex mutex = null;


        public static bool IsOnlyInstance
        {
            get
            {
                bool onlyInstance = Start();

                if (onlyInstance == false)
                    ShowFirstInstance();

                return onlyInstance;
            }
        }

        private static bool Start()
        {
            bool onlyInstance = true;
            string mutexName = String.Format("Local\\{0}", ProgramInfo.AssemblyGuid);

            // if you want your app to be limited to a single instance
            // across ALL SESSIONS (multiple users & terminal services), then use the following line instead:
            // string mutexName = String.Format("Global\\{0}", ProgramInfo.AssemblyGuid);
            //Logger.WriteLog(ProgramInfo.AssemblyGuid);

            try
            {
                // Not owned. What answers the question is whether this process
                // created the named object, which is what the out parameter
                // reports either way - and a mutex belongs to the thread that
                // took it, so asking for ownership here meant the release at
                // shutdown came from the wrong thread and threw
                // "Object synchronization method was called from an
                // unsynchronized block of code" into every session's log. The
                // handle is held for the life of the process, so the object
                // outlives any thread regardless.
                mutex = new Mutex(false, mutexName, out onlyInstance);
            }
            catch (Exception e)
            {
                Logger.WriteLog(e);
                onlyInstance = true;
            }

            //Logger.WriteLog("onlyInstance: " + Convert.ToString(onlyInstance));

            return onlyInstance;
        }

        static public void ShowFirstInstance()
        {
            try
            {
                Win32Interfaces.PostMessage(
                    (IntPtr)Win32Interfaces.HWND_BROADCAST,
                    WM_SHOWFIRSTINSTANCE,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            catch (Exception e)
            {
                Logger.WriteLog(e);
            }
        }

        static public void Stop()
        {
            try
            {
                if (mutex != null)
                {
                    // Closing the handle is the whole of it: the last one closed
                    // takes the named object with it, and the next copy started
                    // then finds nothing and runs.
                    mutex.Dispose();
                    mutex = null;
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog(e);
            }
        }
    }

    static public class ProgramInfo
    {
        static public string AssemblyGuid
        {
            get
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                var attribute = assembly.GetCustomAttributes(typeof(GuidAttribute), true)
                    .OfType<GuidAttribute>()
                    .FirstOrDefault();

                if (attribute != null && !String.IsNullOrWhiteSpace(attribute.Value))
                {
                    return attribute.Value;
                }

                var assemblyIdentity = assembly.FullName ?? assembly.GetName().Name ?? "TataruHelper";

                using var md5 = MD5.Create();
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(assemblyIdentity));
                return new Guid(bytes).ToString("D");
            }
        }
    }
}