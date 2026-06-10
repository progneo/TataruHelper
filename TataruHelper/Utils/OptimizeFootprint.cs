using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper.Services.Logging;

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

        readonly IAppLogger _logger;

        public OptimizeFootprint(IAppLogger logger)
        {
            _logger = logger;
            _keepWorking = true;
        }

        public void Start()
        {
            _keepWorking = true;
            _cts = new CancellationTokenSource();
            _token = _cts.Token;

            _worker = Task.Run(async () =>
            {
                try
                {
                    await EntryPoint();
                }
                catch (Exception e)
                {
                    _logger.WriteLog(e);
                }
            });
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
                        _logger.WriteLog(ex);
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
            catch (Exception e) { _logger.WriteLog(e); }

            try { _worker?.Wait(TimeSpan.FromMilliseconds(500)); }
            catch (Exception e) { _logger.WriteLog(e); }

            try { _cts?.Dispose(); }
            catch (Exception e) { _logger.WriteLog(e); }
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