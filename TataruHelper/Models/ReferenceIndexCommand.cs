using System;
using System.Collections.Generic;

namespace FFXIVTataruHelper
{
    /// <summary>
    /// The command line that rebuilds the index of hand-made translations.
    ///
    /// Preparing a release needs the same index the update button produces, and
    /// for a while a python script alongside carried its own copy of the parsing
    /// rules. Two copies of rules arrived at by trial against a real export do
    /// not stay equal - the two had already drifted on how they counted gendered
    /// lines - so the script is gone and this runs the code the button runs.
    ///
    ///   TataruHelper.exe --build-reference-index
    ///   TataruHelper.exe --build-reference-index --source path\to\xiv_ru_weblate-main
    ///   TataruHelper.exe --build-reference-index --language ru --output path\to\index.db
    /// </summary>
    public sealed class ReferenceIndexCommand
    {
        /// <summary>Where the export is read from: empty means the project on GitHub.</summary>
        public string SourceFolder { get; private set; } = string.Empty;

        public string Language { get; private set; } = string.Empty;

        /// <summary>Where to write; empty means wherever the application reads its index.</summary>
        public string OutputPath { get; private set; } = string.Empty;

        public bool BuildsFromFolder => SourceFolder.Length > 0;

        /// <summary>
        /// Reads the arguments, or null when this is an ordinary launch.
        ///
        /// A value that is missing or looks like the next switch is left empty
        /// rather than swallowing it, so "--language --output x" does not
        /// quietly build an index for a language called "--output".
        /// </summary>
        public static ReferenceIndexCommand Parse(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return null;
            }

            ReferenceIndexCommand command = null;

            for (var i = 0; i < args.Count; i++)
            {
                switch (Normalize(args[i]))
                {
                    case "build-reference-index":
                        command ??= new ReferenceIndexCommand();
                        break;

                    case "source":
                        command ??= new ReferenceIndexCommand();
                        command.SourceFolder = ValueAt(args, i + 1);
                        break;

                    case "language":
                        command ??= new ReferenceIndexCommand();
                        command.Language = ValueAt(args, i + 1);
                        break;

                    case "output":
                        command ??= new ReferenceIndexCommand();
                        command.OutputPath = ValueAt(args, i + 1);
                        break;
                }
            }

            // The other switches only mean anything alongside the one that asks
            // for a build; on their own they are somebody else's arguments.
            return command != null && Asked(args) ? command : null;
        }

        private static bool Asked(IReadOnlyList<string> args)
        {
            foreach (var arg in args)
            {
                if (Normalize(arg) == "build-reference-index")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>"github" is the default rather than a folder, so it is not one.</summary>
        private static string ValueAt(IReadOnlyList<string> args, int index)
        {
            if (index >= args.Count)
            {
                return string.Empty;
            }

            var value = (args[index] ?? string.Empty).Trim();
            if (value.Length == 0 || value.StartsWith("-", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return string.Equals(value, "github", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
        }

        private static string Normalize(string arg)
        {
            return (arg ?? string.Empty).Trim().TrimStart('-', '/').ToLowerInvariant();
        }
    }
}
