using System.Windows;
using System.Windows.Controls;
using FolderIconManager.GUI.Services;
using Microsoft.Win32;

namespace FolderIconManager.GUI.Dialogs;

public partial class SettingsDialog : Window
{
    private readonly UserDataService _userData;
    private readonly ThemeService _themeService;
    private readonly List<string> _defaultSources;

    public SettingsDialog(UserDataService userData, ThemeService? themeService = null)
    {
        InitializeComponent();
        _userData = userData;
        _themeService = themeService ?? new ThemeService();
        
        // Store default sources for reset
        _defaultSources =
        [
            @"%SystemRoot%\System32\shell32.dll",
            @"%SystemRoot%\System32\imageres.dll",
            @"%SystemRoot%\System32\ddores.dll",
            @"%SystemRoot%\System32\netshell.dll",
            @"%SystemRoot%\System32\wmploc.dll",
            @"%SystemRoot%\System32\accessibilitycpl.dll",
            @"%SystemRoot%\System32\moricons.dll",
            @"%SystemRoot%\System32\mmcndmgr.dll",
            @"%SystemRoot%\System32\compstui.dll"
        ];

        LoadSettings();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply title bar theme
        _themeService.ApplyTitleBarTheme(this);
    }

    private void LoadSettings()
    {
        var settings = _userData.Settings;

        // Theme
        ThemeComboBox.SelectedIndex = settings.Theme switch
        {
            AppTheme.System => 0,
            AppTheme.Dark => 1,
            AppTheme.Light => 2,
            _ => 0
        };

        // Scanning
        IncludeSubfoldersCheckBox.IsChecked = settings.IncludeSubfoldersByDefault;
        ShowHiddenFoldersCheckBox.IsChecked = settings.ShowHiddenFolders;
        
        // Set depth limit combo
        if (settings.ScanDepthLimit == null)
            DepthLimitComboBox.SelectedIndex = 0;
        else
        {
            var index = settings.ScanDepthLimit switch
            {
                1 => 1,
                2 => 2,
                3 => 3,
                5 => 4,
                10 => 5,
                _ => 0
            };
            DepthLimitComboBox.SelectedIndex = index;
        }

        // Icons
        LocalRadio.IsChecked = settings.DefaultToLocalIcons;
        ExternalRadio.IsChecked = !settings.DefaultToLocalIcons;
        IconFilenameTextBox.Text = settings.LocalIconFileName;

        // Sources
        SourcesListBox.Items.Clear();
        foreach (var source in settings.SystemIconSources)
        {
            SourcesListBox.Items.Add(source);
        }
    }

    private void SaveSettings()
    {
        var settings = _userData.Settings;

        // Theme
        var newTheme = ThemeComboBox.SelectedIndex switch
        {
            0 => AppTheme.System,
            1 => AppTheme.Dark,
            2 => AppTheme.Light,
            _ => AppTheme.System
        };
        
        if (settings.Theme != newTheme)
        {
            settings.Theme = newTheme;
            _themeService.ApplyTheme(newTheme);
        }

        // Scanning
        settings.IncludeSubfoldersByDefault = IncludeSubfoldersCheckBox.IsChecked == true;
        settings.ShowHiddenFolders = ShowHiddenFoldersCheckBox.IsChecked == true;
        
        settings.ScanDepthLimit = DepthLimitComboBox.SelectedIndex switch
        {
            0 => null,
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 5,
            5 => 10,
            _ => null
        };

        // Icons
        settings.DefaultToLocalIcons = LocalRadio.IsChecked == true;
        var filename = IconFilenameTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(filename))
        {
            if (!filename.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                filename += ".ico";
            settings.LocalIconFileName = filename;
        }

        // Sources
        settings.SystemIconSources.Clear();
        foreach (var item in SourcesListBox.Items)
        {
            if (item is string source)
                settings.SystemIconSources.Add(source);
        }

        _userData.SaveSettings();
    }

    private void AddSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select icon source file",
            Filter = "Executable files (*.exe;*.dll)|*.exe;*.dll|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
        };

        if (dialog.ShowDialog() == true)
        {
            // Convert to environment variable path if possible
            var path = dialog.FileName;
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = "%SystemRoot%" + path.Substring(systemRoot.Length);
            }

            if (!SourcesListBox.Items.Contains(path))
            {
                SourcesListBox.Items.Add(path);
            }
        }
    }

    private void RemoveSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourcesListBox.SelectedItem != null)
        {
            SourcesListBox.Items.Remove(SourcesListBox.SelectedItem);
        }
    }

    private void ResetSourcesButton_Click(object sender, RoutedEventArgs e)
    {
        SourcesListBox.Items.Clear();
        foreach (var source in _defaultSources)
        {
            SourcesListBox.Items.Add(source);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        DialogResult = true;
        Close();
    }
}

