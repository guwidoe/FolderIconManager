using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace FolderIconManager.GUI.Services;

/// <summary>
/// Manages application themes (Dark/Light/System)
/// </summary>
public class ThemeService
{
    public event Action? ThemeChanged;

    public ThemeService()
    {
        // Listen for Windows theme changes
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                // System theme may have changed
                ThemeChanged?.Invoke();
            }
        };
    }

    /// <summary>
    /// Gets whether the system is currently in dark mode
    /// </summary>
    public bool IsSystemDarkMode
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int i && i == 0;
            }
            catch
            {
                return true; // Default to dark
            }
        }
    }

    /// <summary>
    /// Applies the specified theme to the application
    /// </summary>
    public void ApplyTheme(AppTheme theme)
    {
        bool useDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            AppTheme.System => IsSystemDarkMode,
            _ => true
        };

        var resources = Application.Current.Resources;

        if (useDark)
        {
            ApplyDarkTheme(resources);
        }
        else
        {
            ApplyLightTheme(resources);
        }

        ThemeChanged?.Invoke();
    }

    private void ApplyDarkTheme(ResourceDictionary resources)
    {
        // Background colors
        resources["BackgroundDark"] = Color.FromRgb(0x1E, 0x1E, 0x1E);
        resources["BackgroundMedium"] = Color.FromRgb(0x25, 0x25, 0x26);
        resources["BackgroundLight"] = Color.FromRgb(0x2D, 0x2D, 0x30);
        resources["BorderColor"] = Color.FromRgb(0x3F, 0x3F, 0x46);
        resources["TextPrimary"] = Color.FromRgb(0xE0, 0xE0, 0xE0);
        resources["TextSecondary"] = Color.FromRgb(0x90, 0x90, 0x90);

        // Update brushes
        SetBrush(resources, "BackgroundDarkBrush", 0x1E, 0x1E, 0x1E);
        SetBrush(resources, "BackgroundMediumBrush", 0x25, 0x25, 0x26);
        SetBrush(resources, "BackgroundLightBrush", 0x2D, 0x2D, 0x30);
        SetBrush(resources, "BorderBrush", 0x3F, 0x3F, 0x46);
        SetBrush(resources, "TextPrimaryBrush", 0xE0, 0xE0, 0xE0);
        SetBrush(resources, "TextSecondaryBrush", 0x90, 0x90, 0x90);
        SetBrush(resources, "TextTertiaryBrush", 0x60, 0x60, 0x60);
    }

    private void ApplyLightTheme(ResourceDictionary resources)
    {
        // Background colors - light theme
        resources["BackgroundDark"] = Color.FromRgb(0xF5, 0xF5, 0xF5);
        resources["BackgroundMedium"] = Color.FromRgb(0xFF, 0xFF, 0xFF);
        resources["BackgroundLight"] = Color.FromRgb(0xF0, 0xF0, 0xF0);
        resources["BorderColor"] = Color.FromRgb(0xD0, 0xD0, 0xD0);
        resources["TextPrimary"] = Color.FromRgb(0x1E, 0x1E, 0x1E);
        resources["TextSecondary"] = Color.FromRgb(0x60, 0x60, 0x60);

        // Update brushes
        SetBrush(resources, "BackgroundDarkBrush", 0xF5, 0xF5, 0xF5);
        SetBrush(resources, "BackgroundMediumBrush", 0xFF, 0xFF, 0xFF);
        SetBrush(resources, "BackgroundLightBrush", 0xF0, 0xF0, 0xF0);
        SetBrush(resources, "BorderBrush", 0xD0, 0xD0, 0xD0);
        SetBrush(resources, "TextPrimaryBrush", 0x1E, 0x1E, 0x1E);
        SetBrush(resources, "TextSecondaryBrush", 0x60, 0x60, 0x60);
        SetBrush(resources, "TextTertiaryBrush", 0xA0, 0xA0, 0xA0);
    }

    private static void SetBrush(ResourceDictionary resources, string key, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        resources[key] = brush;
    }
}

