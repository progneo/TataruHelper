
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FFXIVTataruHelper
{
    public sealed class LogWriter : IDisposable
    {
        /// <summary>
        /// How large one log may grow before it is rolled over to its ".old"
        /// companion, so each of them costs at most twice this on disk.
        ///
        /// Ten megabytes because the dialogue log is the one that matters for a
        /// bug report and it is written a line at a time all session: an earlier
        /// one reached 38 MB before anybody looked, and at the old five it rolled
        /// often enough to lose the evening somebody was asking about.
        /// </summary>
        const long MaxLogFileSize = 10 * 1024 * 1024;

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
        static readonly string ChatLogFileName = Path.Combine(LogDirectory, "ChatLog.txt");
        static readonly string RawDialogLogFileName = Path.Combine(LogDirectory, "RealtimeRawLog.txt");

        bool _keepWorking;
        bool _disposed;

        readonly RollingLog _appLog;
        readonly RollingLog _chatLog;
        readonly RollingLog _rawDialogLog;
        readonly RollingLog[] _allLogs;

        Task _worker = Task.CompletedTask;

        public LogWriter()
        {
            _keepWorking = true;

            Directory.CreateDirectory(LogDirectory);

            // The application log is opened now, because something may need to
            // be written before anything has happened. The other two are opened
            // on their first line: a session where nobody speaks should not
            // leave an empty dialogue log behind.
            _appLog = new RollingLog(LogFileName, openNow: true);
            _chatLog = new RollingLog(ChatLogFileName, openNow: false);
            _rawDialogLog = new RollingLog(RawDialogLogFileName, openNow: false);
            _allLogs = new[] { _appLog, _chatLog, _rawDialogLog };
        }

        /// <summary>Where to send somebody who is asked for their log.</summary>
        public static string LogFolder => LogDirectory;

        /// <summary>The log itself, for a report to name rather than describe.</summary>
        public static string LogFilePath => LogFileName;

        /// <summary>Every log a report should carry, whether or not it exists yet.</summary>
        public static IReadOnlyList<string> AllLogPaths => new[]
        {
            LogFileName,
            RollingLog.PreviousPathOf(LogFileName),
            ChatLogFileName,
            RollingLog.PreviousPathOf(ChatLogFileName),
            RawDialogLogFileName,
            RollingLog.PreviousPathOf(RawDialogLogFileName)
        };

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
                    _appLog.WriteLine(str);
                    dequeueFlag = true;
                }

                if (Logger.ConsoleLogQueue.TryDequeue(out str))
                {
                    Console.WriteLine(str);
                    dequeueFlag = true;
                }

                if (Logger.ChatLogQueue.TryDequeue(out str))
                {
                    _chatLog.WriteLine(str);
                    dequeueFlag = true;
                }

                if (Logger.RawDialogLogQueue.TryDequeue(out str))
                {
                    _rawDialogLog.WriteLine(str);
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
            foreach (var log in _allLogs)
            {
                log.RollOverIfTooLarge(MaxLogFileSize);
            }
        }

        void ReleaseResources()
        {
            foreach (var log in _allLogs)
            {
                log.Close();
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

        /// <summary>
        /// One log file that keeps the previous generation beside it.
        ///
        /// The three logs used to each carry their own copy of this, and only the
        /// application log's copy had the size check - which is how the dialogue
        /// log came to be tens of megabytes.
        /// </summary>
        private sealed class RollingLog
        {
            private readonly string _path;
            private readonly string _previousPath;

            private StreamWriter _writer;

            public RollingLog(string path, bool openNow)
            {
                _path = path;
                _previousPath = PreviousPathOf(path);

                if (openNow)
                {
                    Open();
                }
            }

            public static string PreviousPathOf(string path)
            {
                var directory = Path.GetDirectoryName(path) ?? string.Empty;

                return Path.Combine(
                    directory,
                    Path.GetFileNameWithoutExtension(path) + "_old" + Path.GetExtension(path));
            }

            public void WriteLine(string line)
            {
                try
                {
                    if (_writer == null)
                    {
                        Open();
                    }

                    _writer?.WriteLine(line);
                    _writer?.Flush();
                }
                catch (Exception e)
                {
                    // Not through Logger: this may be the application log itself,
                    // and a failure to write that would queue a line describing
                    // the failure to write it.
                    Console.WriteLine(e);
                }
            }

            public void RollOverIfTooLarge(long maxBytes)
            {
                if (_writer == null)
                {
                    return;
                }

                try
                {
                    if (_writer.BaseStream.Length < maxBytes)
                    {
                        return;
                    }

                    Close();

                    if (File.Exists(_previousPath))
                    {
                        File.Delete(_previousPath);
                    }

                    if (File.Exists(_path))
                    {
                        File.Move(_path, _previousPath);
                    }

                    Open();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            public void Close()
            {
                try
                {
                    if (_writer == null)
                    {
                        return;
                    }

                    _writer.Flush();
                    _writer.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    _writer = null;
                }
            }

            private void Open()
            {
                _writer = new StreamWriter(_path, true);
            }
        }
    }
}
