using System;
using System.Linq;
using System.Windows;

using Wpf.Ui.Appearance;

namespace FFXIVTataruHelper.Theme;

public enum AppThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2
}

public static class AppThemeService
{
    private const string ColorsDictionaryFileName = "Colors.xaml";
    private const string LightColorsPath = "Theme/Colors.Light.xaml";
    private const string DarkColorsPath = "Theme/Colors.Dark.xaml";

    private static AppThemeMode _currentMode = AppThemeMode.System;

    public static AppThemeMode CurrentMode => _currentMode;

    public static event EventHandler<AppThemeMode> ThemeChanged;

    public static void Apply(AppThemeMode mode)
    {
        _currentMode = mode;

        var effective = mode switch
        {
            AppThemeMode.Light => ApplicationTheme.Light,
            AppThemeMode.Dark => ApplicationTheme.Dark,
            _ => ResolveSystemTheme()
        };

        ApplicationThemeManager.Apply(effective, updateAccent: true);
        SwapAppColors(effective);

        ThemeChanged?.Invoke(null, mode);
    }

    public static AppThemeMode FromInt(int value)
    {
        return value switch
        {
            1 => AppThemeMode.Light,
            2 => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };
    }

    public static int ToInt(AppThemeMode mode)
    {
        return (int)mode;
    }

    private static ApplicationTheme ResolveSystemTheme()
    {
        var system = ApplicationThemeManager.GetSystemTheme();
        return system == SystemTheme.Dark || system == SystemTheme.HCBlack
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }

    private static void SwapAppColors(ApplicationTheme theme)
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        var targetSource = theme == ApplicationTheme.Dark ? DarkColorsPath : LightColorsPath;
        var existing = app.Resources.MergedDictionaries.FirstOrDefault(d =>
            d.Source != null &&
            d.Source.OriginalString.EndsWith(ColorsDictionaryFileName, StringComparison.OrdinalIgnoreCase));

        var replacement = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/" + targetSource, UriKind.Absolute)
        };

        if (existing != null)
        {
            var index = app.Resources.MergedDictionaries.IndexOf(existing);
            app.Resources.MergedDictionaries[index] = replacement;
        }
        else
        {
            app.Resources.MergedDictionaries.Add(replacement);
        }
    }
}