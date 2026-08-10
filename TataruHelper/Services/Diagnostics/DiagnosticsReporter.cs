using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Update;
using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Services.Diagnostics
{
    public interface IDiagnosticsReporter
    {
        /// <summary>The report text, and where a copy of it was written.</summary>
        (string Report, string SavedTo) Collect();
    }

    /// <summary>
    /// Gathers the report from the running application.
    ///
    /// Every read here is wrapped: a diagnostics button that throws while
    /// somebody is trying to report a fault would be the worst of both worlds,
    /// so anything unreadable is reported as unreadable and the rest still gets
    /// out.
    /// </summary>
    public sealed class DiagnosticsReporter : IDiagnosticsReporter
    {
        private readonly Func<GameReadingDiagnostics> _reading;
        private readonly IReferenceIndexUpdateService _referenceIndex;
        private readonly Func<IReadOnlyList<ChatWindowViewModel>> _chatWindows;
        private readonly Func<bool> _referenceTranslationEnabled;
        private readonly Func<string> _uiLanguage;
        private readonly string _appVersion;
        private readonly IAppLogger _logger;

        /// <param name="reading">
        /// Asked for the game side rather than given the reader itself: this
        /// wants one property off it, and taking the whole service would make
        /// every test of the report stand up a memory reader.
        /// </param>
        public DiagnosticsReporter(
            Func<GameReadingDiagnostics> reading,
            IReferenceIndexUpdateService referenceIndex,
            Func<IReadOnlyList<ChatWindowViewModel>> chatWindows,
            Func<bool> referenceTranslationEnabled,
            Func<string> uiLanguage,
            string appVersion,
            IAppLogger logger)
        {
            _reading = reading ?? (() => GameReadingDiagnostics.Unavailable);
            _referenceIndex = referenceIndex;
            _chatWindows = chatWindows ?? (() => Array.Empty<ChatWindowViewModel>());
            _referenceTranslationEnabled = referenceTranslationEnabled ?? (() => false);
            _uiLanguage = uiLanguage ?? (() => string.Empty);
            _appVersion = appVersion ?? string.Empty;
            _logger = logger;
        }

        public (string Report, string SavedTo) Collect()
        {
            var snapshot = new DiagnosticsSnapshot
            {
                AppVersion = _appVersion,
                IsInstalled = LooksInstalled(),
                OperatingSystem = Describe(() => RuntimeInformation.OSDescription),
                IsElevated = IsElevated(),
                UiLanguage = Describe(_uiLanguage),
                Reading = Describe(_reading, GameReadingDiagnostics.Unavailable),
                ReferenceTranslationEnabled = Describe(_referenceTranslationEnabled, false),
                ReferenceIndex = DescribeReferenceIndex(),
                Windows = DescribeWindows(),
                LogPath = Describe(() => LogWriter.LogFilePath)
            };

            var report = DiagnosticsReport.Build(snapshot, DateTime.Now);

            return (report, TryBundle(report));
        }

        private string DescribeReferenceIndex()
        {
            try
            {
                if (_referenceIndex == null || !_referenceIndex.IsSupported)
                {
                    return "not available in this build";
                }

                var state = _referenceIndex.ReadState();
                if (!state.IsInstalled)
                {
                    return "not installed";
                }

                var revision = string.IsNullOrEmpty(state.Revision)
                    ? "shipped with the application"
                    : state.Revision;

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} -> {1}, {2} lines, rules v{3}, revision {4}",
                    string.IsNullOrEmpty(state.SourceLanguage) ? "?" : state.SourceLanguage,
                    string.IsNullOrEmpty(state.Language) ? "?" : state.Language,
                    state.Lines,
                    state.RulesVersion,
                    revision);
            }
            catch (Exception ex)
            {
                _logger?.WriteLog(ex);
                return "could not be read";
            }
        }

        private IReadOnlyList<DiagnosticsWindow> DescribeWindows()
        {
            try
            {
                return _chatWindows()
                    .Where(window => window != null)
                    .Select(window => new DiagnosticsWindow
                    {
                        // The name on screen, not the internal id: they differ,
                        // and a report that calls a window 0 while its owner
                        // calls it 1 costs a message to sort out.
                        Name = string.IsNullOrWhiteSpace(window.Name)
                            ? window.WinId.ToString(CultureInfo.InvariantCulture)
                            : window.Name,
                        Engine = window.SelectedEngine?.EngineName.ToString() ?? string.Empty,
                        FromLanguage = window.CurrentTranslateFromLanguage?.SystemName ?? string.Empty,
                        ToLanguage = window.CurrentTranslateToLanguage?.SystemName ?? string.Empty,
                        TickedCodes = window.ChatCodes?
                            .Where(code => code.IsChecked)
                            .Select(code => code.Code)
                            .ToArray() ?? Array.Empty<string>()
                    })
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger?.WriteLog(ex);
                return Array.Empty<DiagnosticsWindow>();
            }
        }

        /// <summary>
        /// Velopack keeps the application in a "current" folder with its updater
        /// beside it; a portable copy is just the folder.
        /// </summary>
        private static bool LooksInstalled()
        {
            try
            {
                var appDirectory = AppContext.BaseDirectory;
                var parent = Directory.GetParent(appDirectory.TrimEnd(Path.DirectorySeparatorChar));

                return parent != null && File.Exists(Path.Combine(parent.FullName, "Update.exe"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsElevated()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// How much of the end of each log to carry. Enough to hold the evening
        /// somebody is asking about, small enough that the archive can be sent
        /// over a chat service - text of this kind compresses about tenfold.
        /// </summary>
        private const int LogTailBytes = 1024 * 1024;

        /// <summary>
        /// Writes the report and the tail of every log into one archive.
        ///
        /// One file, because the reason to have it is handing it to somebody
        /// else, and a person asked for four files will send one. The logs are
        /// the half that matters most: the report says what the state is now, and
        /// they say what happened - which line arrived under which code, and what
        /// it was turned into.
        /// </summary>
        private string TryBundle(string report)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogWriter.LogFilePath) ?? AppContext.BaseDirectory;
                var path = Path.Combine(directory, "Diagnostics.zip");

                // Replaced rather than added to, so what is sent is this press
                // and not an accumulation of every previous one.
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

                WriteEntry(archive, "report.txt", report);

                foreach (var logPath in LogWriter.AllLogPaths)
                {
                    var tail = TryReadTail(logPath, LogTailBytes);
                    if (tail != null)
                    {
                        WriteEntry(archive, Path.GetFileName(logPath), tail);
                    }
                }

                return path;
            }
            catch (Exception ex)
            {
                _logger?.WriteLog(ex);
                return string.Empty;
            }
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
        }

        /// <summary>
        /// The end of a log, or null when there is nothing to read.
        ///
        /// Opened sharing write access, because the log writer holds these open
        /// for the life of the application - without that this would fail on
        /// every file that matters.
        /// </summary>
        private string TryReadTail(string path, int maxBytes)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                if (stream.Length == 0)
                {
                    return null;
                }

                var truncated = stream.Length > maxBytes;
                if (truncated)
                {
                    stream.Seek(-maxBytes, SeekOrigin.End);
                }

                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();

                if (!truncated)
                {
                    return text;
                }

                // Started mid-line, and a half line at the top reads as a
                // corrupt file rather than as a window onto a longer one.
                var firstBreak = text.IndexOf('\n');
                var body = firstBreak >= 0 ? text.Substring(firstBreak + 1) : text;

                return "[earlier lines omitted]" + Environment.NewLine + body;
            }
            catch (Exception ex)
            {
                _logger?.WriteLog(ex);
                return null;
            }
        }

        private string Describe(Func<string> read)
        {
            return Describe(read, string.Empty);
        }

        private T Describe<T>(Func<T> read, T whenUnreadable)
        {
            try
            {
                return read() ?? whenUnreadable;
            }
            catch (Exception ex)
            {
                _logger?.WriteLog(ex);
                return whenUnreadable;
            }
        }
    }
}
