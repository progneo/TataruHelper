using System;
using System.Collections.ObjectModel;

using Translation.Models;

namespace FFXIVTataruHelper.EventArguments
{
    public class TranslationEngineChangeEventArgs : TatruEventArgs
    {
        public int OldEngine { get; internal set; }

        public int NewEngine { get; internal set; }

        public ReadOnlyCollection<TranslatorLanguage> SupportedLanguages { get; internal set; }

        internal TranslationEngineChangeEventArgs(Object sender) : base(sender) { }
    }
}