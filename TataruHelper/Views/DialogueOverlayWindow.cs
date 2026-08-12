using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.GameMemory;
using FFXIVTataruHelper.Services.UI;

namespace FFXIVTataruHelper
{
    /// <summary>
    /// A copy of the game's dialogue box, holding the translation, put over the
    /// real one.
    ///
    /// Drawn rather than the game's own box being written into: nothing here
    /// touches the game, and the copy simply sits on top of it, following where
    /// the client says the box is and going away when it does.
    /// </summary>
    internal sealed class DialogueOverlayWindow : Window
    {
        /// <summary>
        /// How often the box is asked about. The game moves it only when the
        /// player drags it, but it appears and disappears constantly, and a
        /// copy that lingers after a line is dismissed is the thing a reader
        /// would notice first.
        /// </summary>
        private static readonly TimeSpan FollowInterval = TimeSpan.FromMilliseconds(50);

        private readonly IFFMemoryReaderService _memoryReader;
        private readonly Func<IntPtr> _gameWindow;
        private readonly DispatcherTimer _timer;

        private readonly TextBlock _speaker;
        private readonly TextBlock _line;

        private string _speakerText = string.Empty;
        private string _lineText = string.Empty;

        public DialogueOverlayWindow(IFFMemoryReaderService memoryReader, Func<IntPtr> gameWindow)
        {
            _memoryReader = memoryReader;
            _gameWindow = gameWindow;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            IsHitTestVisible = false;
            ShowActivated = false;
            Visibility = Visibility.Hidden;

            _speaker = new TextBlock
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(14, 0, 0, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _line = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x24, 0x1C)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(26, 12, 26, 12)
            };

            var box = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xEC, 0xDE)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x7E, 0x66)),
                BorderThickness = new Thickness(2),
                Child = _line
            };

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(_speaker, 0);
            Grid.SetRow(box, 1);
            layout.Children.Add(_speaker);
            layout.Children.Add(box);

            Content = layout;

            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = FollowInterval };
            _timer.Tick += (_, __) => Follow();
        }

        /// <summary>The line to show, as it was put on the chat window.</summary>
        public void SetLine(string speaker, string text)
        {
            _speakerText = (speaker ?? string.Empty).Trim().TrimEnd(':');
            _lineText = text ?? string.Empty;

            if (_speakerText.Length > 0 && _lineText.StartsWith(speaker ?? string.Empty, StringComparison.Ordinal))
            {
                _lineText = _lineText.Substring((speaker ?? string.Empty).Length).TrimStart();
            }

            _speaker.Text = _speakerText;
            _speaker.Visibility = _speakerText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            _line.Text = _lineText;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            Visibility = Visibility.Hidden;
        }

        private void Follow()
        {
            if (!GameWindowLocator.TryLocate(_gameWindow(), out var projection))
            {
                Visibility = Visibility.Hidden;
                return;
            }

            var placed = DialogueOverlayPlacement.TryPlace(
                true,
                _memoryReader.IsGameWindowForeground,
                _memoryReader.DialogueBounds,
                projection,
                _lineText,
                out var rect);

            if (!placed)
            {
                Visibility = Visibility.Hidden;
                return;
            }

            Left = rect.Left;
            Top = rect.Top;
            Width = rect.Width;
            Height = rect.Height;

            // The line is set in the box's own proportions rather than at a
            // fixed size: the player's interface scale is already in the
            // rectangle, and text that ignored it would not fit the frame.
            _line.FontSize = Math.Max(10, rect.Height / 9);
            _speaker.FontSize = Math.Max(10, rect.Height / 10);

            if (Visibility != Visibility.Visible)
            {
                Show();
                Visibility = Visibility.Visible;
                MakeClickThrough();
            }
        }

        /// <summary>
        /// Lets the mouse through to the game. Without this the copy swallows
        /// clicks over the dialogue box, which is exactly where the player
        /// clicks to read on.
        /// </summary>
        private void MakeClickThrough()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var style = GetWindowLong(handle, GwlExStyle);
            SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExNoActivate | WsExToolWindow);
        }

        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x20;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x80;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    }
}
