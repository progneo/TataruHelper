using System;
using System.Collections.Concurrent;
using System.Linq;
using Sharlayan.Core;
using Sharlayan.Models.ReadResults;

namespace FFXIVTataruHelper.Services.GameMemory
{
    public sealed class HeuristicDirectDialogReader : IDirectDialogReader
    {
        private readonly ConcurrentQueue<ChatLogItem> _dialogPanelsLog = new ConcurrentQueue<ChatLogItem>();
        private readonly ConcurrentQueue<ChatLogItem> _cutScenesLog = new ConcurrentQueue<ChatLogItem>();
        private readonly ConcurrentQueue<ChatLogItem> _directDialogLog = new ConcurrentQueue<ChatLogItem>();

        public ChatLogResult ExtractDirectDialog(ChatLogResult chatLogResult)
        {
            var result = new ChatLogResult();
            if (chatLogResult == null || chatLogResult.ChatLogItems == null || chatLogResult.ChatLogItems.Count == 0)
            {
                return result;
            }

            var dialogPanel = chatLogResult.ChatLogItems.LastOrDefault(item =>
                IsDialogPanelCandidate(item) && !IsTextEmpty(item));
            var cutsceneText = chatLogResult.ChatLogItems.LastOrDefault(item =>
                IsCutsceneCandidate(item) && !IsTextEmpty(item));

            var dialogRepeat = CheckRepetition(_dialogPanelsLog, dialogPanel);
            var cutsceneRepeat = CheckRepetition(_cutScenesLog, cutsceneText);

            if (CheckChatEquality(dialogPanel, cutsceneText))
            {
                if (dialogPanel != null && !dialogRepeat && !CheckRepetition(_directDialogLog, dialogPanel))
                {
                    result.ChatLogItems.Enqueue(dialogPanel);
                }
            }
            else
            {
                if (dialogPanel != null && !dialogRepeat && !CheckRepetition(_directDialogLog, dialogPanel))
                {
                    result.ChatLogItems.Enqueue(dialogPanel);
                }

                if (cutsceneText != null && !cutsceneRepeat && !CheckRepetition(_directDialogLog, cutsceneText))
                {
                    result.ChatLogItems.Enqueue(cutsceneText);
                }
            }

            return result;
        }

        public bool CheckChatEquality(ChatLogItem item1, ChatLogItem item2)
        {
            if (item1 == null && item2 == null)
            {
                return true;
            }

            if (item1 == null || item2 == null)
            {
                return false;
            }

            var line1 = item1.Line ?? string.Empty;
            var line2 = item2.Line ?? string.Empty;

            if (line1.Contains(":"))
            {
                line1 = line1.Substring(line1.IndexOf(':'));
            }

            if (line2.Contains(":"))
            {
                line2 = line2.Substring(line2.IndexOf(':'));
            }

            var onlyLetters1 = new string(line1.Where(char.IsLetter).ToArray());
            var onlyLetters2 = new string(line2.Where(char.IsLetter).ToArray());

            return onlyLetters1 == onlyLetters2;
        }

        private static bool IsDialogPanelCandidate(ChatLogItem item)
        {
            return item != null && string.Equals(item.Code, "003D", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCutsceneCandidate(ChatLogItem item)
        {
            return item != null && string.Equals(item.Code, "0044", StringComparison.OrdinalIgnoreCase);
        }

        private bool CheckRepetition(ConcurrentQueue<ChatLogItem> log, ChatLogItem item)
        {
            if (item == null)
            {
                return false;
            }

            var repetitionFlag = true;

            if (!string.IsNullOrWhiteSpace(item.Line) && item.Line.Length > 1)
            {
                if (log.TryPeek(out var previousItem))
                {
                    if (!CheckChatEquality(previousItem, item))
                    {
                        while (log.TryDequeue(out _))
                        {
                        }

                        repetitionFlag = false;
                        log.Enqueue(item);
                    }
                }
                else
                {
                    log.Enqueue(item);
                    repetitionFlag = false;
                }
            }

            return repetitionFlag;
        }

        private static bool IsTextEmpty(ChatLogItem chatLogItem)
        {
            if (chatLogItem == null || string.IsNullOrEmpty(chatLogItem.Line))
            {
                return true;
            }

            var index = chatLogItem.Line.IndexOf(':');
            if (index < 0)
            {
                return true;
            }

            return chatLogItem.Line.Length - 1 == index;
        }
    }
}
