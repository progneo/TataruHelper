
using System;
using System.IO;
using System.Threading.Tasks;

namespace FFXIVTataruHelper
{
    public sealed class LogWriter : IDisposable
    {
        const int MaxLogFileSize = 5242880;

        /// <summary>
        /// Where the logs go: beside the settings, in the user's roaming data.
        ///
        /// They used to be written to plain file names, which means relative to
        /// whatever the working directory happened to be. Started from a
        /// shortcut that is the installation folder, which an update replaces
        /// wholesale; started with elevation, which every installed copy is,
        /// Windows makes it C:\WINDOWS\system32 - so an installed Tataru Helper
        /// wrote no log at all, and every line put there to explain itself was
        /// only ever visible to somebody running it out of a build folder.
        /// </summary>
        static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TataruHelper");

        static readonly string LogFileName = Path.Combine(LogDirectory, "Log.txt");
        static readonly string BackUpLogFileName = Path.Combine(LogDirectory, "Log_old.txt");
        static readonly string ChatLogFileName = Path.Combine(LogDirectory, "ChatLog.txt");
        static readonly string RawDialogLogFileName = Path.Combine(LogDirectory, "RealtimeRawLog.txt");

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

            Directory.CreateDirectory(LogDirectory);

            _logStreamWriter = new StreamWriter(LogFileName, true);
            _logTextWriter = _logStreamWriter;
        }

        /// <summary>Where to send somebody who is asked for their log.</summary>
        public static string LogFolder => LogDirectory;

        /// <summary>The log itself, for a report to name rather than describe.</summary>
        public static string LogFilePath => LogFileName;

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