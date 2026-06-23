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
        private static bool mutexOwned;


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
                mutex = new Mutex(true, mutexName, out onlyInstance);
                mutexOwned = onlyInstance;
            }
            catch (Exception e)
            {
                Logger.WriteLog(e);
                onlyInstance = true;
                mutexOwned = false;
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
                    if (mutexOwned)
                    {
                        mutex.ReleaseMutex();
                        mutexOwned = false;
                    }

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