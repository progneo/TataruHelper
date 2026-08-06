using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using Translation.Reference;
using Translation.Settings;

namespace FFXIVTataruHelper.Services.Update
{
    /// <summary>
    /// Runs <see cref="ReferenceIndexCommand"/> without starting the interface.
    ///
    /// The application asks Windows for administrator rights, so a console that
    /// launches it does not get the elevated process back and would see neither
    /// output nor exit code. Run it from a console that is already elevated -
    /// then no new process is made, and this writes where it was called from.
    /// </summary>
    public static class ReferenceIndexCommandRunner
    {
        private const int AttachParentProcess = -1;
        private const int StandardOutputHandle = -11;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        /// <summary>Whether output goes to a console, where a line can be redrawn.</summary>
        private static bool _hasConsole;

        public static int Run(ReferenceIndexCommand command)
        {
            AttachToCallingConsole();

            var settings = TranslationSettingsStorage.Load("TranslationSysSettings.json") ?? new TranslationSettings();

            var language = command.Language.Length > 0 ? command.Language : settings.ReferenceTranslationsLanguage;
            var output = command.OutputPath.Length > 0
                ? Path.GetFullPath(command.OutputPath)
                : SqliteReferenceTranslationSource.Resolve(settings.ReferenceTranslationsPath);

            Console.WriteLine("Building the reference index");
            Console.WriteLine("  source   : " +
                              (command.BuildsFromFolder ? command.SourceFolder : "github (xivrus/xiv_ru_weblate)"));
            Console.WriteLine("  language : " + language);
            Console.WriteLine("  output   : " + output);
            Console.WriteLine();

            var updater = new ReferenceIndexUpdater(null);

            // Not Progress<T>: that hands each report to the dispatcher, and
            // nothing here runs one, so every line of progress arrived at once
            // after the work had finished and the summary had been printed.
            var progress = new ImmediateProgress(Report);

            var result = command.BuildsFromFolder
                ? updater.BuildFromFolder(output, language, command.SourceFolder, progress)

                // No current revision is offered, so this always rebuilds. The
                // point of running it by hand is to get the index as it is now.
                : updater.UpdateAsync(output, language, string.Empty, progress, null, CancellationToken.None)
                    .GetAwaiter().GetResult();

            Console.WriteLine();

            if (result.Outcome != ReferenceUpdateOutcome.Updated)
            {
                Console.Error.WriteLine("Failed: " + result.Detail);
                return 1;
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Wrote {0:N0} lines to {1} ({2:N1} MB){3}",
                result.Lines,
                output,
                new FileInfo(output).Length / (1024d * 1024d),
                result.Detail.Length > 0 ? ", revision " + result.Detail : ", from a folder, so no revision"));

            return 0;
        }

        /// <summary>
        /// Progress arrives often. On a console each line replaces the one
        /// before it; in a redirected build log there is nothing to replace, so
        /// it is thinned out rather than writing a thousand lines nobody reads.
        /// </summary>
        private static void Report(ReferenceUpdateProgress report)
        {
            if (report.Stage == ReferenceUpdateStage.Writing)
            {
                Write("  writing the index...", true);
                return;
            }

            // Reading a folder moves no bytes, and "0 MB" on every line is noise.
            var line = report.Bytes > 0
                ? string.Format(CultureInfo.InvariantCulture, "  {0:N0} MB, {1:N0} sheets, {2:N0} lines",
                    report.Bytes / (1024 * 1024), report.Sheets, report.Lines)
                : string.Format(CultureInfo.InvariantCulture, "  {0:N0} sheets, {1:N0} lines",
                    report.Sheets, report.Lines);

            Write(line, report.Sheets % 250 == 0);
        }

        private static void Write(string line, bool worthALineOfItsOwn)
        {
            if (_hasConsole)
            {
                Console.Write("\r" + line.PadRight(60));
                return;
            }

            if (worthALineOfItsOwn)
            {
                Console.WriteLine(line);
            }
        }

        /// <summary>Reports on the thread doing the work, as it happens.</summary>
        private sealed class ImmediateProgress : IProgress<ReferenceUpdateProgress>
        {
            private readonly Action<ReferenceUpdateProgress> _report;

            public ImmediateProgress(Action<ReferenceUpdateProgress> report)
            {
                _report = report;
            }

            public void Report(ReferenceUpdateProgress value)
            {
                _report(value);
            }
        }

        private static void AttachToCallingConsole()
        {
            try
            {
                // Output already has somewhere to go - a file, a pipe, a build
                // log - because whoever started this passed a handle in.
                // Attaching a console here would take it back off them and
                // write to a window instead, which is how the first attempt at
                // this produced an empty log.
                var inherited = GetStdHandle(StandardOutputHandle);
                if (inherited != IntPtr.Zero && inherited != InvalidHandle)
                {
                    return;
                }

                if (!AttachConsole(AttachParentProcess) && !AllocConsole())
                {
                    return;
                }

                _hasConsole = true;

                // The streams were settled before the console existed, so they
                // point at nothing until they are opened again.
                var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(output);
                Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            }
            catch (IOException)
            {
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int handle);
    }
}
