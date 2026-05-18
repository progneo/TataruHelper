using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FFXIVTataruHelper.Controls;

public partial class FluentColorEditor : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color?),
            typeof(FluentColorEditor),
            new FrameworkPropertyMetadata(
                Colors.Black,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public Color? SelectedColor
    {
        get => (Color?)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private double _hue; // 0..360
    private double _saturation; // 0..1
    private double _value; // 0..1
    private byte _alpha = 255;

    private bool _suppressSync;
    private bool _svDragging;
    private bool _hueDragging;
    private bool _alphaDragging;

    public FluentColorEditor()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyFromColor(SelectedColor ?? Colors.Black);
            SvBox.SizeChanged += (_, _) => UpdateSvCursor();
            HueBar.SizeChanged += (_, _) => UpdateHueCursor();
            AlphaBar.SizeChanged += (_, _) => UpdateAlphaCursor();
        };
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FluentColorEditor self && !self._suppressSync)
        {
            var c = e.NewValue as Color? ?? Colors.Black;
            self.ApplyFromColor(c);
        }
    }

    private void ApplyFromColor(Color c)
    {
        _alpha = c.A;
        RgbToHsv(c.R, c.G, c.B, out _hue, out _saturation, out _value);
        RefreshVisuals(syncFields: true);
    }

    private void RefreshVisuals(bool syncFields)
    {
        var hueColor = HsvToRgb(_hue, 1, 1, 255);
        HueLayer.Fill = new SolidColorBrush(hueColor);

        var opaque = HsvToRgb(_hue, _saturation, _value, 255);
        AlphaGradient.Fill = new LinearGradientBrush(
            Color.FromArgb(0, opaque.R, opaque.G, opaque.B),
            Color.FromArgb(255, opaque.R, opaque.G, opaque.B),
            new Point(0, 0), new Point(1, 0));

        var final = Color.FromArgb(_alpha, opaque.R, opaque.G, opaque.B);
        PreviewSwatch.Fill = new SolidColorBrush(final);

        UpdateSvCursor();
        UpdateHueCursor();
        UpdateAlphaCursor();

        if (syncFields)
        {
            _suppressSync = true;
            RBox.Value = final.R;
            GBox.Value = final.G;
            BBox.Value = final.B;
            ABox.Value = final.A;
            HexBox.Text = $"#{final.A:X2}{final.R:X2}{final.G:X2}{final.B:X2}";
            _suppressSync = false;
        }
    }

    // User-initiated changes call this to push the current HSV state back to the
    // SelectedColor DP (and therefore to the bound source).
    private void CommitToSource()
    {
        var opaque = HsvToRgb(_hue, _saturation, _value, 255);
        var final = Color.FromArgb(_alpha, opaque.R, opaque.G, opaque.B);

        _suppressSync = true;
        SelectedColor = final;
        _suppressSync = false;
    }

    private void UpdateSvCursor()
    {
        if (SvBox.ActualWidth <= 0 || SvBox.ActualHeight <= 0) return;
        var x = _saturation * SvBox.ActualWidth;
        var y = (1 - _value) * SvBox.ActualHeight;
        Canvas.SetLeft(SvCursor, x - SvCursor.Width / 2);
        Canvas.SetTop(SvCursor, y - SvCursor.Height / 2);
    }

    private void UpdateHueCursor()
    {
        if (HueBar.ActualWidth <= 0) return;
        var x = (_hue / 360.0) * HueBar.ActualWidth;
        Canvas.SetLeft(HueCursor, x - HueCursor.Width / 2);
        Canvas.SetTop(HueCursor, 0);
    }

    private void UpdateAlphaCursor()
    {
        if (AlphaBar.ActualWidth <= 0) return;
        var x = (_alpha / 255.0) * AlphaBar.ActualWidth;
        Canvas.SetLeft(AlphaCursor, x - AlphaCursor.Width / 2);
        Canvas.SetTop(AlphaCursor, 0);
    }

    // --- SV box ---
    private void SvBox_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _svDragging = true;
        SvBox.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(SvBox));
    }

    private void SvBox_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_svDragging) UpdateSvFromPoint(e.GetPosition(SvBox));
    }

    private void SvBox_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _svDragging = false;
        SvBox.ReleaseMouseCapture();
    }

    private void UpdateSvFromPoint(Point p)
    {
        var w = SvBox.ActualWidth;
        var h = SvBox.ActualHeight;
        if (w <= 0 || h <= 0) return;
        _saturation = Math.Clamp(p.X / w, 0, 1);
        _value = Math.Clamp(1 - (p.Y / h), 0, 1);
        RefreshVisuals(syncFields: true);
        CommitToSource();
    }

    // --- Hue bar ---
    private void HueBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _hueDragging = true;
        HueBar.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(HueBar));
    }

    private void HueBar_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_hueDragging) UpdateHueFromPoint(e.GetPosition(HueBar));
    }

    private void HueBar_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _hueDragging = false;
        HueBar.ReleaseMouseCapture();
    }

    private void UpdateHueFromPoint(Point p)
    {
        var w = HueBar.ActualWidth;
        if (w <= 0) return;
        _hue = Math.Clamp(p.X / w, 0, 1) * 360.0;
        RefreshVisuals(syncFields: true);
        CommitToSource();
    }

    // --- Alpha bar ---
    private void AlphaBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _alphaDragging = true;
        AlphaBar.CaptureMouse();
        UpdateAlphaFromPoint(e.GetPosition(AlphaBar));
    }

    private void AlphaBar_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_alphaDragging) UpdateAlphaFromPoint(e.GetPosition(AlphaBar));
    }

    private void AlphaBar_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _alphaDragging = false;
        AlphaBar.ReleaseMouseCapture();
    }

    private void UpdateAlphaFromPoint(Point p)
    {
        var w = AlphaBar.ActualWidth;
        if (w <= 0) return;
        _alpha = (byte)Math.Round(Math.Clamp(p.X / w, 0, 1) * 255.0);
        RefreshVisuals(syncFields: true);
        CommitToSource();
    }

    // --- Numeric fields ---
    private void ChannelBox_OnValueChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSync) return;
        byte r = (byte)Math.Clamp(RBox.Value ?? 0, 0, 255);
        byte g = (byte)Math.Clamp(GBox.Value ?? 0, 0, 255);
        byte b = (byte)Math.Clamp(BBox.Value ?? 0, 0, 255);
        byte a = (byte)Math.Clamp(ABox.Value ?? 0, 0, 255);
        _alpha = a;
        RgbToHsv(r, g, b, out _hue, out _saturation, out _value);
        RefreshVisuals(syncFields: false);
        HexBox.Text = $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        CommitToSource();
    }

    // --- Hex ---
    private void HexBox_OnLostFocus(object sender, RoutedEventArgs e) => CommitHex();

    private void HexBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitHex();
            e.Handled = true;
        }
    }

    private void CommitHex()
    {
        var text = HexBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (text.StartsWith("#")) text = text.Substring(1);
        byte a = 255, r = 0, g = 0, b = 0;
        try
        {
            if (text.Length == 6)
            {
                r = byte.Parse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g = byte.Parse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b = byte.Parse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else if (text.Length == 8)
            {
                a = byte.Parse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                r = byte.Parse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g = byte.Parse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b = byte.Parse(text.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
                // restore current
                HexBox.Text = $"#{_alpha:X2}{HsvToRgb(_hue, _saturation, _value, _alpha).R:X2}";
                return;
            }
        }
        catch
        {
            return;
        }

        ApplyFromColor(Color.FromArgb(a, r, g, b));
        CommitToSource();
    }

    // --- HSV/RGB ---
    private static Color HsvToRgb(double h, double s, double v, byte a)
    {
        h = ((h % 360) + 360) % 360;
        var c = v * s;
        var x = c * (1 - Math.Abs(((h / 60.0) % 2) - 1));
        var m = v - c;
        double r1 = 0, g1 = 0, b1 = 0;
        if (h < 60)
        {
            r1 = c;
            g1 = x;
        }
        else if (h < 120)
        {
            r1 = x;
            g1 = c;
        }
        else if (h < 180)
        {
            g1 = c;
            b1 = x;
        }
        else if (h < 240)
        {
            g1 = x;
            b1 = c;
        }
        else if (h < 300)
        {
            r1 = x;
            b1 = c;
        }
        else
        {
            r1 = c;
            b1 = x;
        }

        return Color.FromArgb(a,
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    private static void RgbToHsv(byte rb, byte gb, byte bb, out double h, out double s, out double v)
    {
        double r = rb / 255.0, g = gb / 255.0, b = bb / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        v = max;
        s = max == 0 ? 0 : delta / max;
        if (delta == 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / delta) % 6);
        else if (max == g) h = 60 * (((b - r) / delta) + 2);
        else h = 60 * (((r - g) / delta) + 4);
        if (h < 0) h += 360;
    }
}