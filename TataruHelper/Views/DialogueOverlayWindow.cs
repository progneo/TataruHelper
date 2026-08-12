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
        private readonly Border _box;
        private readonly Border _plate;

        private readonly DialogueOverlayHold _hold = new DialogueOverlayHold();

        private string _speakerText = string.Empty;
        private string _lineText = string.Empty;

        /// <summary>
        /// The widest the box has been seen. The game draws its window at one
        /// size and only grows into it while opening, so anything narrower is
        /// a frame of that animation rather than a box worth covering.
        /// </summary>
        private double _widestSeen;

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

            _box = box;

            // The plate sits over the box's top-left corner rather than above
            // it in a row of its own. Given its own row it took height from the
            // box, which then stopped short of the game's frame and left a
            // strip of the original showing along the top - including the name
            // in the language being translated away.
            _plate = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1C, 0x1A, 0x17)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(12, 2, 12, 2),
                Child = _speaker
            };

            var layout = new Grid();
            layout.Children.Add(box);
            layout.Children.Add(_plate);

            Content = layout;

            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = FollowInterval };
            _timer.Tick += (_, __) => Follow();
        }

        /// <summary>The line to show, as it was put on the chat window.</summary>
        public void SetLine(string speaker, string text)
        {
            var named = (speaker ?? string.Empty).Trim();
            _speakerText = named.TrimEnd(':');
            _lineText = text ?? string.Empty;

            // Taken out wherever it stands rather than only at the front: the
            // marker for a machine translation is put before it, and testing
            // the start left the name both on the plate and in the sentence.
            if (named.Length > 0)
            {
                var at = _lineText.IndexOf(named, StringComparison.Ordinal);
                if (at >= 0)
                {
                    _lineText = (_lineText.Substring(0, at) + _lineText.Substring(at + named.Length)).Trim();
                }
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
                Report("no game window");
                Visibility = Visibility.Hidden;
                return;
            }

            var bounds = _memoryReader.DialogueBounds;
            var foreground = _memoryReader.IsGameWindowForeground;

            var placed = DialogueOverlayPlacement.TryPlace(
                true,
                foreground,
                bounds,
                projection,
                _lineText,
                out var rect);

            if (!_hold.Decide(placed, rect, DateTime.UtcNow, out rect))
            {
                Report(FormattableString.Invariant(
                    $"hidden: foreground={foreground} boundsKnown={bounds.IsKnown} box={bounds.Width}x{bounds.Height} lineChars={_lineText.Length}"));
                Visibility = Visibility.Hidden;

                // The conversation is over, so the line it ended on is not the
                // line anything says next. Held on to, it was showing through
                // the opening moment of the following conversation, before that
                // one's translation had come back - the last thing an NPC said
                // put in the mouth of the next one.
                Forget();
                return;
            }

            // The game opens its window by growing it, and the copy used to
            // follow every frame of that - resizing and re-wrapping the text a
            // dozen times a line. The full size is the one that means anything,
            // so the copy waits for the growing to stop instead of racing it.
            if (rect.Width < _widestSeen * 0.98)
            {
                Report("waiting for the box to finish opening");
                Visibility = Visibility.Hidden;
                return;
            }

            _widestSeen = Math.Max(_widestSeen, rect.Width);

            Report(FormattableString.Invariant(
                $"shown at {rect.Left},{rect.Top} {rect.Width}x{rect.Height}"));

            Left = rect.Left;
            Top = rect.Top;
            Width = rect.Width;
            Height = rect.Height;

            // Everything is set in the box's own proportions rather than at a
            // fixed size: the player's interface scale is already in the
            // rectangle, and text that ignored it would not fit the frame.
            // The figures are the game's frame measured off a screenshot.
            _line.FontSize = Math.Max(10, rect.Height * 0.105);
            _speaker.FontSize = Math.Max(10, rect.Height * 0.095);

            var plateHeight = rect.Height * 0.13;
            _plate.Margin = new Thickness(rect.Width * 0.07, 0, 0, 0);
            _box.Margin = new Thickness(0, plateHeight * 0.75, 0, 0);
            _box.CornerRadius = new CornerRadius(rect.Height * 0.14);
            _line.Margin = new Thickness(
                rect.Width * 0.035, rect.Height * 0.09, rect.Width * 0.035, rect.Height * 0.05);

            if (Visibility != Visibility.Visible)
            {
                Show();
                Visibility = Visibility.Visible;
                MakeClickThrough();
            }
        }

        /// <summary>
        /// Drops the line and what was learned about the box it was in. Both
        /// belong to the conversation that has just ended: the next one starts
        /// blank and fills when its own translation arrives, and the width it
        /// opens to is measured again rather than assumed to match.
        /// </summary>
        private void Forget()
        {
            _lineText = string.Empty;
            _speakerText = string.Empty;
            _line.Text = string.Empty;
            _speaker.Text = string.Empty;
            _widestSeen = 0;
        }

        private string _lastReport = string.Empty;

        /// <summary>
        /// Says why the copy is or is not on screen, once per change rather
        /// than twenty times a second. Watching it decide is the only way to
        /// tell "nothing to show" from "shown somewhere nobody is looking".
        /// </summary>
        private void Report(string state)
        {
            if (!Logger.RawDialogLogEnabled || string.Equals(state, _lastReport, StringComparison.Ordinal))
            {
                return;
            }

            _lastReport = state;
            Logger.WriteRawDialogLog("DialogueOverlay " + state);
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

            try
            {
                var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
                SetWindowLongPtr(
                    handle,
                    GwlExStyle,
                    new IntPtr(style | WsExTransparent | WsExNoActivate | WsExToolWindow));
            }
            catch (EntryPointNotFoundException)
            {
                // Nothing worth stopping over: the copy still shows, it just
                // takes the clicks meant for the game underneath.
                Report("could not make the copy click-through");
            }
        }

        private const int GwlExStyle = -20;
        private const long WsExTransparent = 0x20;
        private const long WsExNoActivate = 0x08000000;
        private const long WsExToolWindow = 0x80;

        // The W suffix matters: 64-bit user32 exports no bare GetWindowLongPtr,
        // and asking for one throws the first time the copy is shown.
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    }
}
