using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderIconManager.Core.Services;

namespace FolderIconManager.GUI.Services;

/// <summary>
/// Caches icon images for display in the tree view
/// </summary>
public class IconCacheService
{
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new();
    private readonly ImageSource _defaultFolderIcon;
    private readonly ImageSource _defaultFolderOpenIcon;

    public IconCacheService()
    {
        // Load default folder icons from shell32
        // Note: shell32.dll index 3 = closed folder, index 4 = open folder in Windows Shell
        _defaultFolderIcon = LoadSystemIcon("shell32.dll", 3) ?? CreatePlaceholderIcon();
        _defaultFolderOpenIcon = LoadSystemIcon("shell32.dll", 4) ?? _defaultFolderIcon;
    }

    /// <summary>
    /// Gets the icon for a folder, using cache when available
    /// </summary>
    public ImageSource GetFolderIcon(string folderPath, string? iconSourcePath, int iconIndex, bool isExpanded = false)
    {
        // No custom icon - return default
        if (string.IsNullOrEmpty(iconSourcePath))
        {
            return isExpanded ? _defaultFolderOpenIcon : _defaultFolderIcon;
        }

        var cacheKey = $"{iconSourcePath}|{iconIndex}";

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Try to load the icon
        var icon = LoadIcon(iconSourcePath, iconIndex);
        if (icon != null)
        {
            _cache[cacheKey] = icon;
            return icon;
        }

        // Failed - return default
        return isExpanded ? _defaultFolderOpenIcon : _defaultFolderIcon;
    }

    /// <summary>
    /// Gets the default folder icon
    /// </summary>
    public ImageSource DefaultFolderIcon => _defaultFolderIcon;

    /// <summary>
    /// Preloads icons for a batch of paths
    /// </summary>
    public async Task PreloadIconsAsync(IEnumerable<(string path, int index)> icons)
    {
        await Task.Run(() =>
        {
            foreach (var (path, index) in icons)
            {
                var cacheKey = $"{path}|{index}";
                if (!_cache.ContainsKey(cacheKey))
                {
                    var icon = LoadIcon(path, index);
                    if (icon != null)
                    {
                        _cache[cacheKey] = icon;
                    }
                }
            }
        });
    }

    /// <summary>
    /// Clears the cache
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    private ImageSource? LoadIcon(string path, int index)
    {
        try
        {
            var resolvedPath = Environment.ExpandEnvironmentVariables(path);
            
            if (!File.Exists(resolvedPath))
                return null;

            // Handle .ico files directly
            if (resolvedPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                return LoadIcoFile(resolvedPath);
            }

            // Extract from PE file (exe, dll)
            return ExtractIconFromPE(resolvedPath, index);
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? LoadIcoFile(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 24; // Small size for tree view
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? ExtractIconFromPE(string path, int index)
    {
        try
        {
            using var extractor = new IconExtractor(path);
            
            // Pass the index directly to GetIcon - it now uses PrivateExtractIcons
            // which matches Windows Shell behavior exactly.
            // Don't clamp based on IconCount as that uses a different enumeration
            // than Windows Shell.
            using var icon = extractor.GetIcon(index, 32);
            if (icon == null)
                return null;

            return ConvertIconToImageSource(icon);
        }
        catch
        {
            return null;
        }
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

    private ImageSource? LoadSystemIcon(string dllName, int index)
    {
        try
        {
            var systemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), dllName);
            return ExtractIconFromPE(systemPath, index);
        }
        catch
        {
            return null;
        }
    }

    private ImageSource CreatePlaceholderIcon()
    {
        // Create a simple folder-like placeholder
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 100));
            brush.Freeze();
            context.DrawRectangle(brush, null, new Rect(2, 6, 20, 14));
            context.DrawRectangle(brush, null, new Rect(2, 4, 8, 4));
        }

        var bitmap = new RenderTargetBitmap(24, 24, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}

