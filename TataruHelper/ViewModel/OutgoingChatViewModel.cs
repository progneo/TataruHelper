using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using FFXIVTataruHelper.Services.OutgoingChat;
using FFXIVTataruHelper.UIModel;

using Translation.Core;
using Translation.OutgoingChat;

namespace FFXIVTataruHelper.ViewModel
{
    public sealed class OutgoingChatViewModel : INotifyPropertyChanged
    {
        private const int RecentTellsLimit = 8;

        private readonly IOutgoingChatService _outgoingChatService;
        private readonly WebTranslator _webTranslator;
        private readonly TataruUIModel _tataruUIModel;
        private readonly OutgoingChatSettings _settings;
        private readonly IMessageSanitizer _sanitizer;
        private readonly ObservableCollection<string> _recentTells = new ObservableCollection<string>();
        private CancellationTokenSource _activeCts;

        public event PropertyChangedEventHandler PropertyChanged;

        public OutgoingChatViewModel(
            IOutgoingChatService outgoingChatService,
            WebTranslator webTranslator,
            TataruUIModel tataruUIModel,
            IMessageSanitizer sanitizer)
        {
            _outgoingChatService = outgoingChatService ?? throw new ArgumentNullException(nameof(outgoingChatService));
            _webTranslator = webTranslator ?? throw new ArgumentNullException(nameof(webTranslator));
            _tataruUIModel = tataruUIModel ?? throw new ArgumentNullException(nameof(tataruUIModel));
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));

            _settings = _tataruUIModel.OutgoingChat ?? new OutgoingChatSettings();
            _tataruUIModel.OutgoingChat = _settings;

            Channels = new ObservableCollection<ChatChannel>(BuildChannelList());
            SelectedChannel = _settings.DefaultChannel;
            TellTarget = _settings.LastTellTarget ?? string.Empty;
            PrependChannelCommand = _settings.PrependChannelCommand;
            AppendOriginalInParentheses = _settings.AppendOriginalInParentheses;
            RestoreClipboardAfterDelay = _settings.RestoreClipboardAfterDelay;
            ClipboardRestoreDelaySeconds = _settings.ClipboardRestoreDelaySeconds;

            CopyCommand = new RelayCommand(_ => _ = ExecuteCopyAsync(), _ => CanCopy);
            CancelCommand = new RelayCommand(_ => CancelActive(), _ => IsBusy);
        }

        public ObservableCollection<ChatChannel> Channels { get; }

        public ObservableCollection<string> RecentTells => _recentTells;

        public ICommand CopyCommand { get; }

        public ICommand CancelCommand { get; }

        private ChatChannel _selectedChannel;

        public ChatChannel SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                if (_selectedChannel == value) return;
                _selectedChannel = value;
                _settings.DefaultChannel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTellSelected));
            }
        }

        public bool IsTellSelected => _selectedChannel == ChatChannel.Tell;

        private string _tellTarget = string.Empty;

        public string TellTarget
        {
            get => _tellTarget;
            set
            {
                var v = value ?? string.Empty;
                if (_tellTarget == v) return;
                _tellTarget = v;
                _settings.LastTellTarget = v;
                OnPropertyChanged();
            }
        }

        private string _messageText = string.Empty;

        public string MessageText
        {
            get => _messageText;
            set
            {
                var v = value ?? string.Empty;
                if (_messageText == v) return;
                _messageText = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Utf8ByteLength));
                OnPropertyChanged(nameof(CanCopy));
            }
        }

        public int Utf8ByteLength => _sanitizer.Utf8ByteLength(_messageText);

        private bool _prependChannelCommand;

        public bool PrependChannelCommand
        {
            get => _prependChannelCommand;
            set
            {
                if (_prependChannelCommand == value) return;
                _prependChannelCommand = value;
                _settings.PrependChannelCommand = value;
                OnPropertyChanged();
            }
        }

        private bool _appendOriginalInParentheses;

        public bool AppendOriginalInParentheses
        {
            get => _appendOriginalInParentheses;
            set
            {
                if (_appendOriginalInParentheses == value) return;
                _appendOriginalInParentheses = value;
                _settings.AppendOriginalInParentheses = value;
                OnPropertyChanged();
            }
        }

        private bool _restoreClipboardAfterDelay;

        public bool RestoreClipboardAfterDelay
        {
            get => _restoreClipboardAfterDelay;
            set
            {
                if (_restoreClipboardAfterDelay == value) return;
                _restoreClipboardAfterDelay = value;
                _settings.RestoreClipboardAfterDelay = value;
                OnPropertyChanged();
            }
        }

        private int _clipboardRestoreDelaySeconds;

        public int ClipboardRestoreDelaySeconds
        {
            get => _clipboardRestoreDelaySeconds;
            set
            {
                if (_clipboardRestoreDelaySeconds == value) return;
                _clipboardRestoreDelaySeconds = value;
                _settings.ClipboardRestoreDelaySeconds = value;
                OnPropertyChanged();
            }
        }

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCopy));
                (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _statusMessage = string.Empty;

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                var v = value ?? string.Empty;
                if (_statusMessage == v) return;
                _statusMessage = v;
                OnPropertyChanged();
            }
        }

        public bool CanCopy => !IsBusy && !string.IsNullOrWhiteSpace(_messageText);

        public void CancelActive()
        {
            try { _activeCts?.Cancel(); }
            catch { }
        }

        private async Task ExecuteCopyAsync()
        {
            if (!CanCopy) return;

            CancelActive();
            _activeCts = new CancellationTokenSource();
            IsBusy = true;
            StatusMessage = "Translating…";

            try
            {
                var window = _tataruUIModel?.ChatWindows?.FirstOrDefault();
                var engineName = window != null ? window.TranslationEngineName : TranslationEngineName.GoogleTranslate;
                var engine = _webTranslator.TranslationEngines?.FirstOrDefault(e => e.EngineName == engineName)
                             ?? _webTranslator.TranslationEngines?.FirstOrDefault();

                if (engine == null)
                {
                    StatusMessage = "No translation engine available.";
                    return;
                }

                var gameLang = window?.FromLanguague
                               ?? engine.SupportedLanguages.FirstOrDefault();
                var userLang = window?.ToLanguague
                               ?? engine.SupportedLanguages.FirstOrDefault();

                if (gameLang == null || userLang == null)
                {
                    StatusMessage = "No language pair configured.";
                    return;
                }

                var request = new OutgoingChatRequest
                {
                    Text = _messageText,
                    Channel = SelectedChannel,
                    TellTarget = TellTarget,
                    Engine = engine,
                    FromLanguage = userLang,
                    ToLanguage = gameLang,
                    PrependChannelCommand = PrependChannelCommand,
                    AppendOriginalInParentheses = AppendOriginalInParentheses,
                    RestoreClipboardAfterDelay = RestoreClipboardAfterDelay,
                    ClipboardRestoreDelaySeconds = ClipboardRestoreDelaySeconds
                };

                var result = await _outgoingChatService
                    .TranslateAndCopyAsync(request, _activeCts.Token)
                    .ConfigureAwait(true);

                if (result.IsSuccess)
                {
                    StatusMessage = "Copied to clipboard.";
                    if (SelectedChannel == ChatChannel.Tell && !string.IsNullOrWhiteSpace(TellTarget))
                    {
                        AddRecentTell(TellTarget.Trim());
                    }
                }
                else
                {
                    StatusMessage = FormatFailure(result);
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string FormatFailure(OutgoingChatResult result)
        {
            switch (result.Kind)
            {
                case OutgoingChatResultKind.EmptyInput:
                    return "Message is empty.";
                case OutgoingChatResultKind.InvalidTellTarget:
                    return "Tell target must be 'Firstname Lastname' or 'Firstname Lastname@World'.";
                case OutgoingChatResultKind.TranslationFailed:
                    return "Translation failed: " + result.ErrorMessage;
                case OutgoingChatResultKind.ClipboardFailed:
                    return "Could not access clipboard.";
                default:
                    return result.ErrorMessage;
            }
        }

        private void AddRecentTell(string target)
        {
            var existing = _recentTells.IndexOf(target);
            if (existing >= 0)
            {
                _recentTells.RemoveAt(existing);
            }

            _recentTells.Insert(0, target);

            while (_recentTells.Count > RecentTellsLimit)
            {
                _recentTells.RemoveAt(_recentTells.Count - 1);
            }
        }

        private static IEnumerable<ChatChannel> BuildChannelList()
        {
            return new[]
            {
                ChatChannel.Say, ChatChannel.Yell, ChatChannel.Shout, ChatChannel.Party, ChatChannel.Alliance,
                ChatChannel.FreeCompany, ChatChannel.NoviceNetwork, ChatChannel.Linkshell1, ChatChannel.Linkshell2,
                ChatChannel.Linkshell3, ChatChannel.Linkshell4, ChatChannel.Linkshell5, ChatChannel.Linkshell6,
                ChatChannel.Linkshell7, ChatChannel.Linkshell8, ChatChannel.CrossWorldLinkshell1,
                ChatChannel.CrossWorldLinkshell2, ChatChannel.CrossWorldLinkshell3,
                ChatChannel.CrossWorldLinkshell4, ChatChannel.CrossWorldLinkshell5,
                ChatChannel.CrossWorldLinkshell6, ChatChannel.CrossWorldLinkshell7,
                ChatChannel.CrossWorldLinkshell8, ChatChannel.Tell, ChatChannel.Echo, ChatChannel.Emote
            };
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Predicate<object> _canExecute;

            public event EventHandler CanExecuteChanged;

            public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

            public void Execute(object parameter) => _execute(parameter);

            public void RaiseCanExecuteChanged() =>
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}