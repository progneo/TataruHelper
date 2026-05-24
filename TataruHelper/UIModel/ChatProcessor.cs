// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;

using Translation.Core;

namespace FFXIVTataruHelper
{
    public class ChatProcessor
    {
        #region **Events.

        public event AsyncEventHandler<ChatMessageArrivedEventArgs> TextArrived
        {
            add { this._TextArrivedArrived.Register(value); }
            remove { this._TextArrivedArrived.Unregister(value); }
        }

        private AsyncEvent<ChatMessageArrivedEventArgs> _TextArrivedArrived;

        #endregion

        #region **Properties.

        public ReadOnlyCollection<TranslationEngine> TranslationEngines
        {
            get { return _WebTranslator.TranslationEngines; }
        }

        public ReadOnlyCollection<ChatMsgType> AllChatCodes
        {
            get
            {
                return new ReadOnlyCollection<ChatMsgType>(_AllChatCodes);
            }
        }

        #endregion

        #region **LocalVariables.

        WebTranslator _WebTranslator;

        DateTime _LastTranslationTime;

        List<ChatMsgType> _AllChatCodes;

        List<string> MsgBlackList;

        List<string> ChatCodesWithNickNames;

        ChatMessageFilter _ChatMessageFilter;

        readonly ISettingsStore _SettingsStore;
        readonly IAppLogger _Logger;

        private readonly object _translationBufferSync = new object();
        private readonly Dictionary<string, TranslationBufferState> _translationBufferStates;

        private readonly int _translationContextBufferWindowMs;
        private readonly int _translationContextMaxBatchSize;
        private readonly string _translationBatchDelimiter;

        #endregion

        public ChatProcessor(WebTranslator webTranslator, ISettingsStore settingsStore, IAppLogger logger)
        {
            this._TextArrivedArrived =
                new AsyncEvent<ChatMessageArrivedEventArgs>(this.EventErrorHandler, "TranslationArrived");

            _SettingsStore = settingsStore;
            _Logger = logger;

            _translationBufferStates = new Dictionary<string, TranslationBufferState>(StringComparer.Ordinal);
            _translationContextBufferWindowMs = Math.Max(0, GlobalSettings.TranslationContextBufferWindowMs);
            _translationContextMaxBatchSize = Math.Max(1, GlobalSettings.TranslationContextMaxBatchSize);
            _translationBatchDelimiter = string.IsNullOrEmpty(GlobalSettings.TranslationContextBatchDelimiter)
                ? "\n<<<TATARU_TRANSLATION_SEGMENT>>>\n"
                : GlobalSettings.TranslationContextBatchDelimiter;

            _AllChatCodes = Helper.LoadJsonData<List<ChatMsgType>>(_SettingsStore.ChatCodesFilePath);

            _WebTranslator = webTranslator;

            MsgBlackList = new List<string>();

            Init();

            _LastTranslationTime = DateTime.UtcNow;
        }

        private void Init()
        {
            var tmpMsgBlackList = new List<string>();
            tmpMsgBlackList.Add("Triple Triad matches not allowed in current area.");
            tmpMsgBlackList.Add("Triple Triad matches allowed in current area.");
            tmpMsgBlackList.Add("You have left the sanctuary.");
            tmpMsgBlackList.Add("You have entered a sanctuary.");
            tmpMsgBlackList.Add("Updating online status to Away from Keyboard.");
            tmpMsgBlackList.Add("Updating online status. No longer away from keyboard.");

            MsgBlackList = Helper.LoadJsonData<List<string>>(_SettingsStore.BlackListPath);
            if (MsgBlackList == null)
                MsgBlackList = new List<string>();

            foreach (var st in tmpMsgBlackList)
            {
                if (!MsgBlackList.Contains(st))
                    MsgBlackList.Add(st);
            }

            MsgBlackList = MsgBlackList.Distinct().ToList();

            Helper.SaveJson(MsgBlackList, _SettingsStore.BlackListPath);

            for (int i = 0; i < MsgBlackList.Count; i++)
            {
                MsgBlackList[i] = Helper.ClearBlackListString(MsgBlackList[i]);
            }

            var tmpChatCodesWithNickNames = new List<string>(31);
            tmpChatCodesWithNickNames.Add("003D");
            tmpChatCodesWithNickNames.Add("0044");
            tmpChatCodesWithNickNames.Add("F03D");
            tmpChatCodesWithNickNames.Add("F044");
            tmpChatCodesWithNickNames.Add("0048");
            tmpChatCodesWithNickNames.Add("000A");
            tmpChatCodesWithNickNames.Add("000B");
            tmpChatCodesWithNickNames.Add("000E");
            tmpChatCodesWithNickNames.Add("000D");
            tmpChatCodesWithNickNames.Add("001D");
            tmpChatCodesWithNickNames.Add("001C");
            tmpChatCodesWithNickNames.Add("0018");
            tmpChatCodesWithNickNames.Add("001E");
            tmpChatCodesWithNickNames.Add("000F");
            tmpChatCodesWithNickNames.Add("0010");
            tmpChatCodesWithNickNames.Add("0011");
            tmpChatCodesWithNickNames.Add("0012");
            tmpChatCodesWithNickNames.Add("0013");
            tmpChatCodesWithNickNames.Add("0014");
            tmpChatCodesWithNickNames.Add("0015");
            tmpChatCodesWithNickNames.Add("0016");
            tmpChatCodesWithNickNames.Add("0017");
            tmpChatCodesWithNickNames.Add("001B");
            tmpChatCodesWithNickNames.Add("0025");
            tmpChatCodesWithNickNames.Add("0065");
            tmpChatCodesWithNickNames.Add("0066");
            tmpChatCodesWithNickNames.Add("0067");
            tmpChatCodesWithNickNames.Add("0068");
            tmpChatCodesWithNickNames.Add("0069");
            tmpChatCodesWithNickNames.Add("006A");
            tmpChatCodesWithNickNames.Add("006B");

            ChatCodesWithNickNames = Helper.LoadJsonData<List<string>>(_SettingsStore.IgnoreNickNameChatCodesPath);
            if (ChatCodesWithNickNames == null)
                ChatCodesWithNickNames = new List<string>();

            foreach (var st in tmpChatCodesWithNickNames)
            {
                if (!ChatCodesWithNickNames.Contains(st))
                    ChatCodesWithNickNames.Add(st);
            }

            ChatCodesWithNickNames = ChatCodesWithNickNames.Distinct().ToList();

            Helper.SaveJson(ChatCodesWithNickNames, _SettingsStore.IgnoreNickNameChatCodesPath);

            _ChatMessageFilter = new ChatMessageFilter(MsgBlackList, ChatCodesWithNickNames);
        }

        public async Task OnFFChatMessageArrived(ChatMessageArrivedEventArgs ea)
        {
            ChatMsgType msgType = new ChatMsgType();

            if (_ChatMessageFilter.ShouldTranslate(ea.ChatMessage.Text))
                await ProcessChatMsg(ea, msgType);

            if (CmdArgsStatus.LogAllChat || CmdArgsStatus.LogPlotChat)
                _Logger.WriteChatLog(String.Format("{0} {1}: {2}", ea.ChatMessage.TimeStamp, ea.ChatMessage.Code,
                    ea.ChatMessage.Text));
        }

        public async Task<TranslationResult> Translate(string inSentence, TranslationEngine translationEngine,
            TranslatorLanguague fromLang, TranslatorLanguague toLang, string chatCode,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string nickName;
            string sentenceToTranslate;
            _ChatMessageFilter.TrySplitNickname(chatCode, inSentence, out nickName, out sentenceToTranslate);

            var batchKey = BuildTranslationBatchKey(chatCode, nickName, translationEngine, fromLang, toLang);
            var result = await QueueForBatchedTranslation(
                sentenceToTranslate,
                batchKey,
                translationEngine,
                fromLang,
                toLang,
                cancellationToken);

            if (result.IsSuccess && nickName.Length > 0 && result.Text.Length > 0)
            {
                return TranslationResult.Success(result.Engine, nickName + " " + result.Text);
            }

            return result;
        }

        private async Task<TranslationResult> QueueForBatchedTranslation(
            string sentenceToTranslate,
            string batchKey,
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            TranslatorLanguague toLang,
            CancellationToken cancellationToken)
        {
            var request = new BufferedTranslationRequest(sentenceToTranslate, cancellationToken);
            List<BufferedTranslationRequest> batchToFlush = null;

            lock (_translationBufferSync)
            {
                TranslationBufferState state;
                if (!_translationBufferStates.TryGetValue(batchKey, out state))
                {
                    state = new TranslationBufferState();
                    _translationBufferStates[batchKey] = state;
                }

                state.PendingRequests.Add(request);

                if (state.PendingRequests.Count >= _translationContextMaxBatchSize)
                {
                    batchToFlush = TakeBatch(state, _translationContextMaxBatchSize);
                    if (state.PendingRequests.Count == 0)
                    {
                        CancelDelayedFlush(state);
                        _translationBufferStates.Remove(batchKey);
                    }
                    else
                    {
                        EnsureDelayedFlushScheduled(batchKey, state, translationEngine, fromLang, toLang);
                    }
                }
                else
                {
                    EnsureDelayedFlushScheduled(batchKey, state, translationEngine, fromLang, toLang);
                }
            }

            if (batchToFlush != null)
            {
                _ = FlushBatchAsync(batchKey, batchToFlush, translationEngine, fromLang, toLang);
            }

            return await request.CompletionSource.Task;
        }

        private void EnsureDelayedFlushScheduled(
            string batchKey,
            TranslationBufferState state,
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            TranslatorLanguague toLang)
        {
            if (state.DelayCts != null)
                return;

            state.DelayCts = new CancellationTokenSource();
            var delayToken = state.DelayCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (_translationContextBufferWindowMs > 0)
                    {
                        await Task.Delay(_translationContextBufferWindowMs, delayToken);
                    }

                    List<BufferedTranslationRequest> batch;

                    lock (_translationBufferSync)
                    {
                        TranslationBufferState currentState;
                        if (!_translationBufferStates.TryGetValue(batchKey, out currentState))
                        {
                            return;
                        }

                        if (!ReferenceEquals(currentState.DelayCts, state.DelayCts))
                        {
                            return;
                        }

                        currentState.DelayCts = null;

                        if (currentState.PendingRequests.Count == 0)
                        {
                            _translationBufferStates.Remove(batchKey);
                            return;
                        }

                        batch = TakeBatch(currentState, _translationContextMaxBatchSize);

                        if (currentState.PendingRequests.Count > 0)
                        {
                            EnsureDelayedFlushScheduled(batchKey, currentState, translationEngine, fromLang, toLang);
                        }
                        else
                        {
                            _translationBufferStates.Remove(batchKey);
                        }
                    }

                    await FlushBatchAsync(batchKey, batch, translationEngine, fromLang, toLang);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private async Task FlushBatchAsync(
            string batchKey,
            List<BufferedTranslationRequest> requests,
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            TranslatorLanguague toLang)
        {
            if (requests == null || requests.Count == 0)
            {
                return;
            }

            var activeRequests = requests.Where(x => !x.CancellationToken.IsCancellationRequested).ToList();
            if (activeRequests.Count == 0)
            {
                foreach (var request in requests)
                {
                    request.CompletionSource.TrySetCanceled(request.CancellationToken);
                }

                return;
            }

            try
            {
                if (activeRequests.Count == 1)
                {
                    await TranslateSingleRequest(activeRequests[0], translationEngine, fromLang, toLang);
                }
                else
                {
                    await TranslateBatchRequests(activeRequests, translationEngine, fromLang, toLang);
                }

                foreach (var request in requests)
                {
                    if (!activeRequests.Contains(request) && !request.CompletionSource.Task.IsCompleted)
                    {
                        request.CompletionSource.TrySetCanceled(request.CancellationToken);
                    }
                }
            }
            catch (Exception exception)
            {
                _Logger.WriteLog($"Failed to flush translation batch '{batchKey}'.");
                _Logger.WriteLog(exception);

                foreach (var request in requests)
                {
                    if (request.CancellationToken.IsCancellationRequested)
                    {
                        request.CompletionSource.TrySetCanceled(request.CancellationToken);
                    }
                    else
                    {
                        request.CompletionSource.TrySetResult(TranslationResult.Failure(
                            translationEngine?.EngineName ?? default,
                            TranslationFailureKind.ProviderException,
                            exception.Message));
                    }
                }
            }
        }

        private async Task TranslateSingleRequest(
            BufferedTranslationRequest request,
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            TranslatorLanguague toLang)
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                request.CompletionSource.TrySetCanceled(request.CancellationToken);
                return;
            }

            var result = await _WebTranslator.TranslateAsync(
                request.InputText,
                translationEngine,
                fromLang,
                toLang,
                CancellationToken.None);

            request.CompletionSource.TrySetResult(result);
        }

        private async Task TranslateBatchRequests(
            IReadOnlyList<BufferedTranslationRequest> requests,
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            TranslatorLanguague toLang)
        {
            var combinedInput =
                string.Join(_translationBatchDelimiter, requests.Select(x => x.InputText ?? string.Empty));

            var combinedResult = await _WebTranslator.TranslateAsync(
                combinedInput,
                translationEngine,
                fromLang,
                toLang,
                CancellationToken.None);

            if (combinedResult.IsSuccess && TrySplitBatchedTranslation(
                    combinedResult.Text,
                    _translationBatchDelimiter,
                    requests.Count,
                    out var translatedSegments))
            {
                for (int i = 0; i < requests.Count; i++)
                {
                    requests[i].CompletionSource.TrySetResult(
                        TranslationResult.Success(combinedResult.Engine, translatedSegments[i]));
                }

                return;
            }

            for (int i = 0; i < requests.Count; i++)
            {
                var result = await _WebTranslator.TranslateAsync(
                    requests[i].InputText ?? string.Empty,
                    translationEngine,
                    fromLang,
                    toLang,
                    CancellationToken.None);

                requests[i].CompletionSource.TrySetResult(result);
            }
        }

        internal static bool TrySplitBatchedTranslation(
            string combinedTranslation,
            string delimiter,
            int expectedCount,
            out List<string> translatedSegments)
        {
            translatedSegments = null;

            if (expectedCount <= 0)
            {
                translatedSegments = new List<string>();
                return true;
            }

            if (string.IsNullOrEmpty(combinedTranslation) || string.IsNullOrEmpty(delimiter))
            {
                return false;
            }

            var segments = combinedTranslation.Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(x => x ?? string.Empty)
                .ToList();

            if (segments.Count != expectedCount)
            {
                return false;
            }

            translatedSegments = segments;
            return true;
        }

        private static List<BufferedTranslationRequest> TakeBatch(TranslationBufferState state, int maxBatchSize)
        {
            var count = Math.Min(maxBatchSize, state.PendingRequests.Count);
            var batch = state.PendingRequests.Take(count).ToList();
            state.PendingRequests.RemoveRange(0, count);
            return batch;
        }

        private static void CancelDelayedFlush(TranslationBufferState state)
        {
            if (state.DelayCts == null)
            {
                return;
            }

            state.DelayCts.Cancel();
            state.DelayCts.Dispose();
            state.DelayCts = null;
        }

        private static string BuildTranslationBatchKey(
            string chatCode,
            string nickName,
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            TranslatorLanguague toLang)
        {
            return string.Join("|",
                new[]
                {
                    chatCode ?? string.Empty, nickName ?? string.Empty,
                    translationEngine?.EngineName.ToString() ?? string.Empty,
                    fromLang?.LanguageCode ?? string.Empty, toLang?.LanguageCode ?? string.Empty
                });
        }

        private async Task ProcessChatMsg(ChatMessageArrivedEventArgs ea, ChatMsgType msgType)
        {
            switch (msgType.MsgType)
            {
                default:
                    {
                        var translation = new ChatMessageArrivedEventArgs(ea);

                        await _TextArrivedArrived.InvokeAsync(translation);

                        break;
                    }
            }
        }

        private void EventErrorHandler(string evname, Exception ex)
        {
            string text = evname + Environment.NewLine + Convert.ToString(ex);
            _Logger.WriteLog(text);
        }

        private sealed class TranslationBufferState
        {
            public List<BufferedTranslationRequest> PendingRequests { get; } = new List<BufferedTranslationRequest>();

            public CancellationTokenSource DelayCts { get; set; }
        }

        private sealed class BufferedTranslationRequest
        {
            public string InputText { get; }

            public CancellationToken CancellationToken { get; }

            public TaskCompletionSource<TranslationResult> CompletionSource { get; }

            public BufferedTranslationRequest(string inputText, CancellationToken cancellationToken)
            {
                InputText = inputText ?? string.Empty;
                CancellationToken = cancellationToken;
                CompletionSource =
                    new TaskCompletionSource<TranslationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (cancellationToken.CanBeCanceled)
                {
                    var registration =
                        cancellationToken.Register(() => CompletionSource.TrySetCanceled(cancellationToken));
                    CompletionSource.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
                }
            }
        }
    }
}