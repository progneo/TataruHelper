using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FFXIVTataruHelper.Views.Controls;

public partial class SecretField : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(SecretField),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal));

    public static readonly DependencyProperty MaskedProperty = DependencyProperty.Register(
        nameof(Masked), typeof(string), typeof(SecretField),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsRevealedProperty = DependencyProperty.Register(
        nameof(IsRevealed), typeof(bool), typeof(SecretField),
        new PropertyMetadata(false));

    public SecretField()
    {
        InitializeComponent();
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Masked
    {
        get => (string)GetValue(MaskedProperty);
        set => SetValue(MaskedProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    private void OnValueKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        e.Handled = true;
    }
}