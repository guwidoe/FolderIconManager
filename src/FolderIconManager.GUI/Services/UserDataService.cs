using System.IO;
using System.Text.Json;

namespace FolderIconManager.GUI.Services;

/// <summary>
/// Manages user data: settings, favorites, recent icons, window state
/// </summary>
public class UserDataService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FolderIconManager");

    private static readonly string SettingsPath = Path.Combine(AppDataPath, "settings.json");
    private static readonly string FavoritesPath = Path.Combine(AppDataPath, "favorites.json");
    private static readonly string RecentPath = Path.Combine(AppDataPath, "recent.json");

    private UserSettings _settings = new();
    private List<IconReference> _favorites = [];
    private List<IconReference> _recentIcons = [];

    public UserDataService()
    {
        EnsureAppDataFolder();
        Load();
    }

    #region Settings

    public UserSettings Settings => _settings;

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    #endregion

    #region Favorites

    public IReadOnlyList<IconReference> Favorites => _favorites;

    public void AddFavorite(IconReference icon)
    {
        // Remove if already exists
        _favorites.RemoveAll(f => f.Equals(icon));
        _favorites.Insert(0, icon);
        SaveFavorites();
    }

    public void RemoveFavorite(IconReference icon)
    {
        _favorites.RemoveAll(f => f.Equals(icon));
        SaveFavorites();
    }

    public bool IsFavorite(IconReference icon)
    {
        return _favorites.Any(f => f.Equals(icon));
    }

    private void SaveFavorites()
    {
        try
        {
            var json = JsonSerializer.Serialize(_favorites, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FavoritesPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    #endregion

    #region Recent Icons

    public IReadOnlyList<IconReference> RecentIcons => _recentIcons;

    public void AddRecentIcon(IconReference icon)
    {
        // Remove if already exists
        _recentIcons.RemoveAll(r => r.Equals(icon));
        _recentIcons.Insert(0, icon);
        
        // Keep only last 50
        if (_recentIcons.Count > 50)
            _recentIcons = _recentIcons.Take(50).ToList();
        
        SaveRecent();
    }

    private void SaveRecent()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentIcons, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(RecentPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    #endregion

    #region Private Methods

    private void EnsureAppDataFolder()
    {
        if (!Directory.Exists(AppDataPath))
        {
            Directory.CreateDirectory(AppDataPath);
        }
    }

    private void Load()
    {
        // Load settings
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                _settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                _settings = new UserSettings();
            }
        }

        // Load favorites
        if (File.Exists(FavoritesPath))
        {
            try
            {
                var json = File.ReadAllText(FavoritesPath);
                _favorites = JsonSerializer.Deserialize<List<IconReference>>(json) ?? [];
            }
            catch
            {
                _favorites = [];
            }
        }

        // Load recent
        if (File.Exists(RecentPath))
        {
            try
            {
                var json = File.ReadAllText(RecentPath);
                _recentIcons = JsonSerializer.Deserialize<List<IconReference>>(json) ?? [];
            }
            catch
            {
                _recentIcons = [];
            }
        }
    }

    #endregion
}

/// <summary>
/// User settings for the application
/// </summary>
public class UserSettings
{
    public int? ScanDepthLimit { get; set; } = null; // null = unlimited
    public bool IncludeSubfoldersByDefault { get; set; } = true;
    public bool ShowHiddenFolders { get; set; } = false;
    public bool DefaultToLocalIcons { get; set; } = true;
    public string LocalIconFileName { get; set; } = "folder.ico";

    // Window state
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool? WindowMaximized { get; set; }
    
    // Last used path
    public string? LastFolderPath { get; set; }

    // System icon sources
    public List<string> SystemIconSources { get; set; } =
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
}

/// <summary>
/// Reference to an icon (for favorites/recent)
/// </summary>
public class IconReference : IEquatable<IconReference>
{
    public string FilePath { get; set; } = "";
    public int Index { get; set; }
    public string? DisplayName { get; set; }

    public bool Equals(IconReference? other)
    {
        if (other is null) return false;
        return string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase) 
               && Index == other.Index;
    }

    public override bool Equals(object? obj) => Equals(obj as IconReference);
    public override int GetHashCode() => HashCode.Combine(FilePath?.ToLowerInvariant(), Index);
}

