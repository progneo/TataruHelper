// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com


using System;
using System.IO;
using System.Threading.Tasks;

namespace FFXIVTataruHelper
{
    class LogWriter
    {
        const int _MaxLogFileSize = 5242880;

        bool _KeepWorking;
        TextWriter _logTextWriter;
        StreamWriter _logStreamWriter;

        TextWriter chatsw = null;

        string LogFileName = @"Log.txt";
        string BackUpLogFileName = @"Log_old.txt";

        string ChatLogFileName = @"ChatLog.txt";

        Task _worker = Task.CompletedTask;

        public LogWriter()
        {
            _KeepWorking = true;

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

            while (_KeepWorking)
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
                    if (chatsw == null)
                        chatsw = new StreamWriter(ChatLogFileName, true);

                    chatsw.WriteLine(str);
                    chatsw.Flush();
                    dequeueFlag = true;
                }

                if (!dequeueFlag)
                {
                    // Block until a producer signals or we're told to stop.
                    // 500 ms cap is a safety net in case a Set was missed.
                    Logger.QueueSignal.WaitOne(500);

                    if (_KeepWorking)
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
                if (_logStreamWriter.BaseStream.Length >= _MaxLogFileSize)
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
                    catch (Exception) { }
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
                    _logTextWriter.Close();
                }

                if (chatsw != null)
                {
                    chatsw.Flush();
                    chatsw.Close();
                }
            }
            catch { }
        }

        public void Stop()
        {
            _KeepWorking = false;
            try { Logger.QueueSignal.Set(); }
            catch { }

            try
            {
                _worker?.Wait(TimeSpan.FromMilliseconds(500));
            }
            catch { }
        }
    }
}