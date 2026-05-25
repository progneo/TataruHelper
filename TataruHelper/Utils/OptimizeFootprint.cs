// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FFXIVTataruHelper
{
    public class OptimizeFootprint
    {
        [DllImport("KERNEL32.DLL", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        static extern bool SetProcessWorkingSetSize32(IntPtr pProcess, int dwMinimumWorkingSetSize,
            int dwMaximumWorkingSetSize);

        [DllImport("KERNEL32.DLL", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        static extern bool SetProcessWorkingSetSize64(IntPtr pProcess, long dwMinimumWorkingSetSize,
            long dwMaximumWorkingSetSize);

        const int InitialDelayMs = 10000;
        const int OptimizeIntervalMs = 60000;

        bool _keepWorking;

        CancellationTokenSource _cts;
        CancellationToken _token;

        Task _worker = Task.CompletedTask;

        public OptimizeFootprint()
        {
            _keepWorking = true;
        }

        public void Start()
        {
            _keepWorking = true;
            _cts = new CancellationTokenSource();
            _token = _cts.Token;

            _worker = Task.Factory.StartNew(async () =>
            {
                try
                {
                    await EntryPoint();
                }
                catch (Exception e)
                {
                    Logger.WriteLog(e);
                }
            }, TaskCreationOptions.LongRunning).Unwrap();
        }

        async Task EntryPoint()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                _keepWorking = false;

            while (_keepWorking)
            {
                if (_keepWorking)
                {
                    try
                    {
                        await Task.Delay(InitialDelayMs, _token);
                    }
                    catch (OperationCanceledException) { }
                }

                if (_keepWorking)
                {
                    try
                    {
                        FlushMemory();
                        MinimizeFootprint();
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLog(ex);
                    }
                }

                if (_keepWorking)
                {
                    try
                    {
                        await Task.Delay(OptimizeIntervalMs, _token);
                    }
                    catch (OperationCanceledException) { }
                }
            }
        }

        public void Stop()
        {
            if (!_keepWorking)
                return;

            _keepWorking = false;

            try { _cts?.Cancel(); }
            catch (Exception e) { Logger.WriteLog(e); }

            try { _worker?.Wait(TimeSpan.FromMilliseconds(500)); }
            catch (Exception e) { Logger.WriteLog(e); }

            try { _cts?.Dispose(); }
            catch (Exception e) { Logger.WriteLog(e); }
        }

        private void FlushMemory()
        {
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (IntPtr.Size == 8)
                {
                    SetProcessWorkingSetSize64(Process.GetCurrentProcess().Handle, -1, -1);
                }
                else
                {
                    SetProcessWorkingSetSize32(Process.GetCurrentProcess().Handle, -1, -1);
                }
            }
        }

        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        private void MinimizeFootprint()
        {
            using (var proc = Process.GetCurrentProcess())
            {
                EmptyWorkingSet(proc.Handle);
            }
        }
    }
}