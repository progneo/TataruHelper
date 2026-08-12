using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

                // Said outright: stretched to fill the frame the text came out
                // sitting near the bottom of it, where the game starts it just
                // under the name.
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // The game's own frame, lifted from it: the shape has notches, a
            // shadow and an embossed rim that a drawn rectangle only ever
            // approximates. Stretched whole rather than cut into corners and
            // edges, because the window is always the same design size and only
            // the interface scale changes it - so the proportions never move.
            var box = new Border
            {
                Background = new ImageBrush(
                    new BitmapImage(new Uri("pack://application:,,,/Resources/DialogueFrame.png")))
                {
                    Stretch = Stretch.Fill
                },
                Child = _line
            };

            _box = box;

            // The plate sits over the box's top-left corner rather than above
            // it in a row of its own. Given its own row it took height from the
            // box, which then stopped short of the game's frame and left a
            // strip of the original showing along the top - including the name
            // in the language being translated away.
            // The game does not put the name in a box. It lays it on a dark
            // strip that fades away to the right, so the strip ends wherever
            // the name does without ever showing an edge.
            var plateWash = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            plateWash.GradientStops.Add(new GradientStop(Color.FromArgb(0xE6, 0x14, 0x12, 0x10), 0));
            plateWash.GradientStops.Add(new GradientStop(Color.FromArgb(0xE0, 0x14, 0x12, 0x10), 0.62));
            plateWash.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x14, 0x12, 0x10), 1));

            _plate = new Border
            {
                Background = plateWash,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
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
            // Fractions of the box, measured off the game's own frame at an
            // interface scale of 150%: the name sits at 0.083 across and 0.04
            // down, the line starts at 0.088 across and 0.225 down.
            _line.FontSize = Math.Max(10, rect.Height * 0.098);
            _speaker.FontSize = Math.Max(10, rect.Height * 0.092);

            // The strip is given room past the name for the fade to happen in.
            // Sized to the name alone it ended in a hard edge a few pixels
            // after the last letter - a dark tab, where the game has a wash.
            _plate.Margin = new Thickness(rect.Width * 0.083, rect.Height * 0.035, 0, 0);
            _plate.Padding = new Thickness(rect.Width * 0.012, rect.Height * 0.005, rect.Width * 0.14, 0);

            // Wide enough to bury the game's own name underneath, whatever it
            // says. A strip cut to the translated name left the English one
            // showing past it - two names side by side, which is worse than
            // either alone.
            _plate.MinWidth = rect.Width * 0.30;
            _line.Margin = new Thickness(
                rect.Width * 0.088, rect.Height * 0.225, rect.Width * 0.075, rect.Height * 0.06);

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
