using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using FFXIVTataruHelper.Factories;
using FFXIVTataruHelper.UIModel;

using Translation.OutgoingChat;

namespace FFXIVTataruHelper.ViewModel.Shell
{
    public sealed class OutgoingChatSettingsViewModel : INotifyPropertyChanged
    {
        private readonly TataruUIModel _uiModel;
        private readonly IOutgoingChatWindowFactory _windowFactory;

        public OutgoingChatSettingsViewModel(TataruUIModel uiModel, IOutgoingChatWindowFactory windowFactory)
        {
            _uiModel = uiModel ?? throw new ArgumentNullException(nameof(uiModel));
            _windowFactory = windowFactory;

            Channels = new ObservableCollection<ChatChannel>(BuildChannelList());
            OpenWindowCommand = new TataruUICommand(_ => TryOpenWindow());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ChatChannel> Channels { get; }

        public ICommand OpenWindowCommand { get; }

        private OutgoingChatSettings Settings => _uiModel.OutgoingChat;

        public bool PrependChannelCommand
        {
            get => Settings.PrependChannelCommand;
            set
            {
                if (Settings.PrependChannelCommand == value) return;
                Settings.PrependChannelCommand = value;
                OnPropertyChanged();
            }
        }

        public bool AppendOriginalInParentheses
        {
            get => Settings.AppendOriginalInParentheses;
            set
            {
                if (Settings.AppendOriginalInParentheses == value) return;
                Settings.AppendOriginalInParentheses = value;
                OnPropertyChanged();
            }
        }

        public bool RestoreClipboardAfterDelay
        {
            get => Settings.RestoreClipboardAfterDelay;
            set
            {
                if (Settings.RestoreClipboardAfterDelay == value) return;
                Settings.RestoreClipboardAfterDelay = value;
                OnPropertyChanged();
            }
        }

        public int ClipboardRestoreDelaySeconds
        {
            get => Settings.ClipboardRestoreDelaySeconds;
            set
            {
                if (Settings.ClipboardRestoreDelaySeconds == value) return;
                Settings.ClipboardRestoreDelaySeconds = value;
                OnPropertyChanged();
            }
        }

        public ChatChannel DefaultChannel
        {
            get => Settings.DefaultChannel;
            set
            {
                if (Settings.DefaultChannel == value) return;
                Settings.DefaultChannel = value;
                OnPropertyChanged();
            }
        }

        private void TryOpenWindow()
        {
            if (_windowFactory == null) return;
            var window = _windowFactory.GetOrCreate();
            window.ShowAndActivate();
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
    }
}