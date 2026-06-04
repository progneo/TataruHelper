// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com


using System;
using System.IO;
using System.Threading.Tasks;

namespace FFXIVTataruHelper
{
    class LogWriter : IDisposable
    {
        const int MaxLogFileSize = 5242880;

        const string LogFileName = @"Log.txt";
        const string BackUpLogFileName = @"Log_old.txt";
        const string ChatLogFileName = @"ChatLog.txt";
        const string RawDialogLogFileName = @"RealtimeRawLog.txt";

        bool _keepWorking;
        bool _disposed;
        TextWriter _logTextWriter;
        StreamWriter _logStreamWriter;

        TextWriter _chatWriter;
        TextWriter _rawDialogWriter;

        Task _worker = Task.CompletedTask;

        public LogWriter()
        {
            _keepWorking = true;

            _logStreamWriter = new StreamWriter(LogFileName, true);
            _logTextWriter = _logStreamWriter;
        }

        public void StartWriting()
        {
            _worker = Task.Factory.StartNew(() =>
            {
                try
                {
                    EntryPoint();
                }
                catch (Exception e)
                {
                    Logger.WriteLog(e);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void EntryPoint()
        {
            Logger.WriteLog("Started Logging");

            string str;

            while (_keepWorking)
            {
                bool dequeueFlag = false;

                if (Logger.LogQueue.TryDequeue(out str))
                {
                    _logTextWriter.WriteLine(str);
                    _logTextWriter.Flush();
                    dequeueFlag = true;
                }

                if (Logger.ConsoleLogQueue.TryDequeue(out str))
                {
                    Console.WriteLine(str);
                    dequeueFlag = true;
                }

                if (Logger.ChatLogQueue.TryDequeue(out str))
                {
                    if (_chatWriter == null)
                        _chatWriter = new StreamWriter(ChatLogFileName, true);

                    _chatWriter.WriteLine(str);
                    _chatWriter.Flush();
                    dequeueFlag = true;
                }

                if (Logger.RawDialogLogQueue.TryDequeue(out str))
                {
                    if (_rawDialogWriter == null)
                        _rawDialogWriter = new StreamWriter(RawDialogLogFileName, true);

                    _rawDialogWriter.WriteLine(str);
                    _rawDialogWriter.Flush();
                    dequeueFlag = true;
                }

                if (!dequeueFlag)
                {
                    Logger.QueueSignal.WaitOne(500);

                    if (_keepWorking)
                    {
                        LimitLogFileSize();
                    }
                }
            }

            ReleaseResources();
        }

        private void LimitLogFileSize()
        {
            if (_logStreamWriter != null && _logTextWriter != null)
            {
                if (_logStreamWriter.BaseStream.Length >= MaxLogFileSize)
                {
                    try
                    {
                        _logTextWriter.Flush();
                        _logTextWriter.Close();
                        _logTextWriter.Dispose();

                        _logStreamWriter.Close();
                        _logStreamWriter.Dispose();

                        if (File.Exists(BackUpLogFileName))
                            File.Delete(BackUpLogFileName);

                        if (File.Exists(LogFileName))
                        {
                            File.Copy(LogFileName, BackUpLogFileName);
                            File.Delete(LogFileName);
                        }

                        _logStreamWriter = new StreamWriter(LogFileName, true);
                        _logTextWriter = _logStreamWriter;
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLog(e);
                    }
                }
            }
        }

        void ReleaseResources()
        {
            try
            {
                if (_logTextWriter != null)
                {
                    _logTextWriter.Flush();
                    _logTextWriter.Dispose();
                    _logTextWriter = null;
                }

                if (_chatWriter != null)
                {
                    _chatWriter.Flush();
                    _chatWriter.Dispose();
                    _chatWriter = null;
                }

                if (_rawDialogWriter != null)
                {
                    _rawDialogWriter.Flush();
                    _rawDialogWriter.Dispose();
                    _rawDialogWriter = null;
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog(e);
            }
        }

        public void Stop()
        {
            _keepWorking = false;

            try
            {
                Logger.QueueSignal.Set();
            }
            catch (Exception e)
            {
                Logger.WriteLog(e);
            }

            try
            {
                _worker?.Wait(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception e)
            {
                Logger.WriteLog(e);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            Stop();

            ReleaseResources();
        }
    }
}