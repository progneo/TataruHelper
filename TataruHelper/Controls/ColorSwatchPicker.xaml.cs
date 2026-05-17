using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FFXIVTataruHelper.Controls;

public partial class ColorSwatchPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color?),
            typeof(ColorSwatchPicker),
            new FrameworkPropertyMetadata(
                Colors.Transparent,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public static readonly DependencyProperty SwatchWidthProperty =
        DependencyProperty.Register(
            nameof(SwatchWidth),
            typeof(double),
            typeof(ColorSwatchPicker),
            new PropertyMetadata(28.0));

    public static readonly DependencyProperty SwatchHeightProperty =
        DependencyProperty.Register(
            nameof(SwatchHeight),
            typeof(double),
            typeof(ColorSwatchPicker),
            new PropertyMetadata(28.0));

    public static readonly DependencyProperty SelectedColorBrushProperty =
        DependencyProperty.Register(
            nameof(SelectedColorBrush),
            typeof(Brush),
            typeof(ColorSwatchPicker),
            new PropertyMetadata(Brushes.Transparent));

    public ColorSwatchPicker()
    {
        InitializeComponent();
    }

    public Color? SelectedColor
    {
        get => (Color?)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public double SwatchWidth
    {
        get => (double)GetValue(SwatchWidthProperty);
        set => SetValue(SwatchWidthProperty, value);
    }

    public double SwatchHeight
    {
        get => (double)GetValue(SwatchHeightProperty);
        set => SetValue(SwatchHeightProperty, value);
    }

    public Brush SelectedColorBrush
    {
        get => (Brush)GetValue(SelectedColorBrushProperty);
        private set => SetValue(SelectedColorBrushProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorSwatchPicker self)
        {
            var color = e.NewValue as Color? ?? Colors.Transparent;
            self.SelectedColorBrush = new SolidColorBrush(color);
        }
    }
}