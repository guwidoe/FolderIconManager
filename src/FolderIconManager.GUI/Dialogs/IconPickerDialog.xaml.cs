using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderIconManager.Core.Services;
using FolderIconManager.GUI.Services;
using Microsoft.Win32;

namespace FolderIconManager.GUI.Dialogs;

public partial class IconPickerDialog : Window
{
    private readonly UserDataService _userData;
    private readonly List<IconItem> _allSystemIcons = [];
    private readonly ObservableCollection<IconItem> _displayedIcons = [];
    private readonly ObservableCollection<IconItem> _browseIcons = [];
    private readonly ObservableCollection<IconItem> _recentIcons = [];
    
    private IconItem? _selectedIcon;
    private string? _browseFilePath;
    private double _iconSize = 48;

    public IconPickerDialog(UserDataService userData)
    {
        InitializeComponent();
        _userData = userData;
        
        IconsGrid.ItemsSource = _displayedIcons;
        BrowseIconsGrid.ItemsSource = _browseIcons;
        RecentIconsGrid.ItemsSource = _recentIcons;

        // Populate source combo
        foreach (var source in _userData.Settings.SystemIconSources)
        {
            var name = Path.GetFileName(Environment.ExpandEnvironmentVariables(source));
            SourceComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = source });
        }
        
        if (SourceComboBox.Items.Count > 0)
            SourceComboBox.SelectedIndex = 0;

        // Load recent icons
        LoadRecentIcons();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply title bar theme based on current theme setting
        var themeService = new ThemeService();
        themeService.ApplyTitleBarTheme(this);
    }

    #region Properties

    public string? SelectedIconPath { get; private set; }
    public int SelectedIconIndex { get; private set; }
    public bool UseLocalStorage => LocalRadio.IsChecked == true;

    #endregion

    #region Event Handlers

    private async void SourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceComboBox.SelectedItem is ComboBoxItem item && item.Tag is string sourcePath)
        {
            await LoadSystemIconsAsync(sourcePath);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterIcons();
    }

    private void IconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _iconSize = Math.Round(e.NewValue);
        if (IconSizeText != null)
            IconSizeText.Text = _iconSize.ToString();
        
        // Update icon sizes in all collections
        foreach (var icon in _displayedIcons)
            icon.IconSize = _iconSize;
        foreach (var icon in _browseIcons)
            icon.IconSize = _iconSize;
    }

    private void IconItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Just capture the click
    }

    private void IconItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is IconItem item)
        {
            SelectIcon(item);
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select icon source file",
            Filter = "Icon files (*.ico)|*.ico|Executable files (*.exe;*.dll)|*.exe;*.dll|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
        };

        if (dialog.ShowDialog() == true)
        {
            _browseFilePath = dialog.FileName;
            BrowsePathBox.Text = dialog.FileName;
            await LoadBrowseIconsAsync(dialog.FileName);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIcon != null)
        {
            SelectedIconPath = _selectedIcon.SourcePath;
            SelectedIconIndex = _selectedIcon.Index;
            
            // Add to recent
            _userData.AddRecentIcon(new IconReference
            {
                FilePath = SelectedIconPath,
                Index = SelectedIconIndex
            });
            
            DialogResult = true;
            Close();
        }
    }

    #endregion

    #region Private Methods

    private async Task LoadSystemIconsAsync(string sourcePath)
    {
        _allSystemIcons.Clear();
        _displayedIcons.Clear();

        var expandedPath = Environment.ExpandEnvironmentVariables(sourcePath);
        if (!File.Exists(expandedPath))
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                using var extractor = new IconExtractor(expandedPath);
                var icons = new List<IconItem>();

                for (int i = 0; i < extractor.IconCount; i++)
                {
                    try
                    {
                        using var icon = extractor.GetIcon(i, 48);
                        if (icon != null)
                        {
                            var imageSource = ConvertIconToImageSource(icon);
                            if (imageSource != null)
                            {
                                icons.Add(new IconItem
                                {
                                    Index = i,
                                    Image = imageSource,
                                    SourcePath = sourcePath,
                                    IconSize = _iconSize
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Skip icons that can't be loaded
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    _allSystemIcons.AddRange(icons);
                    FilterIcons();
                });
            }
            catch
            {
                // Failed to load icons
            }
        });
    }

    private async Task LoadBrowseIconsAsync(string filePath)
    {
        _browseIcons.Clear();

        if (!File.Exists(filePath))
            return;

        await Task.Run(() =>
        {
            try
            {
                // Handle .ico files
                if (filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                            bitmap.EndInit();
                            bitmap.Freeze();

                            _browseIcons.Add(new IconItem
                            {
                                Index = 0,
                                Image = bitmap,
                                SourcePath = filePath,
                                IconSize = _iconSize
                            });
                        }
                        catch { }
                    });
                    return;
                }

                // Handle PE files
                using var extractor = new IconExtractor(filePath);
                var icons = new List<IconItem>();

                for (int i = 0; i < extractor.IconCount; i++)
                {
                    try
                    {
                        using var icon = extractor.GetIcon(i, 48);
                        if (icon != null)
                        {
                            var imageSource = ConvertIconToImageSource(icon);
                            if (imageSource != null)
                            {
                                icons.Add(new IconItem
                                {
                                    Index = i,
                                    Image = imageSource,
                                    SourcePath = filePath,
                                    IconSize = _iconSize
                                });
                            }
                        }
                    }
                    catch { }
                }

                Dispatcher.Invoke(() =>
                {
                    foreach (var icon in icons)
                        _browseIcons.Add(icon);
                });
            }
            catch { }
        });
    }

    private void LoadRecentIcons()
    {
        _recentIcons.Clear();

        foreach (var recent in _userData.RecentIcons.Take(20))
        {
            try
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(recent.FilePath);
                if (!File.Exists(expandedPath))
                    continue;

                ImageSource? imageSource = null;

                if (expandedPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(expandedPath, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 48;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    imageSource = bitmap;
                }
                else
                {
                    using var extractor = new IconExtractor(expandedPath);
                    var index = recent.Index;
                    if (index >= extractor.IconCount)
                        index = 0;
                    
                    using var icon = extractor.GetIcon(index, 48);
                    if (icon != null)
                    {
                        imageSource = ConvertIconToImageSource(icon);
                    }
                }

                if (imageSource != null)
                {
                    _recentIcons.Add(new IconItem
                    {
                        Index = recent.Index,
                        Image = imageSource,
                        SourcePath = recent.FilePath,
                        IconSize = 64
                    });
                }
            }
            catch { }
        }
    }

    private void FilterIcons()
    {
        _displayedIcons.Clear();
        
        var searchText = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
        
        foreach (var icon in _allSystemIcons)
        {
            if (string.IsNullOrEmpty(searchText) || icon.Index.ToString().Contains(searchText))
            {
                icon.IconSize = _iconSize;
                _displayedIcons.Add(icon);
            }
        }
    }

    private void SelectIcon(IconItem item)
    {
        // Deselect previous
        if (_selectedIcon != null)
            _selectedIcon.IsSelected = false;

        // Select new
        _selectedIcon = item;
        item.IsSelected = true;

        // Update preview
        PreviewImage.Source = item.Image;
        PreviewSourceText.Text = Path.GetFileName(Environment.ExpandEnvironmentVariables(item.SourcePath));
        PreviewIndexText.Text = $"Index: {item.Index}";

        ApplyButton.IsEnabled = true;
    }

    private static ImageSource? ConvertIconToImageSource(System.Drawing.Icon icon)
    {
        try
        {
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}

/// <summary>
/// Represents a single icon item in the picker grid
/// </summary>
public class IconItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private double _iconSize = 48;

    public int Index { get; set; }
    public ImageSource? Image { get; set; }
    public string SourcePath { get; set; } = "";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public double IconSize
    {
        get => _iconSize;
        set { _iconSize = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

