using System;
using System.Collections.Generic;
using System.Linq;

namespace FFXIVTataruHelper
{
    public class ChatMessageFilter
    {
        private readonly HashSet<string> _blackList;
        private readonly HashSet<string> _chatCodesWithNickNames;

        public ChatMessageFilter(IEnumerable<string> blackList, IEnumerable<string> chatCodesWithNickNames)
        {
            _blackList = new HashSet<string>(
                (blackList ?? Enumerable.Empty<string>()).Select(NormalizeBlackListEntry),
                StringComparer.Ordinal);

            _chatCodesWithNickNames = new HashSet<string>(
                chatCodesWithNickNames ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool ShouldTranslate(string text)
        {
            return !_blackList.Contains(NormalizeBlackListEntry(text));
        }

        public bool TrySplitNickname(string chatCode, string input, out string nickName, out string textToTranslate)
        {
            nickName = String.Empty;
            textToTranslate = input ?? String.Empty;

            if (!_chatCodesWithNickNames.Contains(chatCode))
                return false;

            if (String.IsNullOrEmpty(textToTranslate))
                return false;

            var separatorIndex = textToTranslate.IndexOf(":");
            if (separatorIndex <= 0)
                return false;

            if (!LooksLikeSpeakerName(textToTranslate.Substring(0, separatorIndex)))
                return false;

            separatorIndex++;
            nickName = textToTranslate.Substring(0, separatorIndex);
            textToTranslate = textToTranslate.Remove(0, separatorIndex);
            return true;
        }

        /// <summary>
        /// Guards the speaker split against colons that belong to the sentence.
        ///
        /// Cutscene subtitles carry no speaker at all, so a line such as
        /// "For the sake of all, I beseech thee: deliver us from this fate!" had
        /// everything before the colon treated as a name and left untranslated.
        /// A character name is short and carries no sentence punctuation.
        /// </summary>
        internal static bool LooksLikeSpeakerName(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            candidate = candidate.Trim();

            const int maxSpeakerNameLength = 40;
            if (candidate.Length > maxSpeakerNameLength)
                return false;

            foreach (var c in candidate)
            {
                if (c == ',' || c == '.' || c == '!' || c == '?' || c == ';')
                    return false;
            }

            const int maxSpeakerNameWords = 5;
            return candidate.Split(' ').Length <= maxSpeakerNameWords;
        }

        public static string NormalizeBlackListEntry(string text)
        {
            return Helper.ClearBlackListString(text ?? String.Empty);
        }
    }
}
