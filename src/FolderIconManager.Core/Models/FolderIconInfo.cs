namespace FolderIconManager.Core.Models;

/// <summary>
/// Represents complete information about a folder's custom icon configuration
/// </summary>
public class FolderIconInfo
{
    /// <summary>
    /// The folder path that has the custom icon
    /// </summary>
    public required string FolderPath { get; init; }

    /// <summary>
    /// Path to the desktop.ini file
    /// </summary>
    public string DesktopIniPath => Path.Combine(FolderPath, "desktop.ini");

    /// <summary>
    /// The current icon resource (may point to external file)
    /// </summary>
    public IconResource? CurrentIconResource { get; init; }

    /// <summary>
    /// The local icon file path (if icon has been extracted)
    /// </summary>
    public string? LocalIconPath { get; set; }

    /// <summary>
    /// Whether a local icon file exists
    /// </summary>
    public bool HasLocalIcon => !string.IsNullOrEmpty(LocalIconPath) && File.Exists(LocalIconPath);

    /// <summary>
    /// Whether the current icon source is already local (in the same folder)
    /// </summary>
    public bool IsAlreadyLocal
    {
        get
        {
            if (CurrentIconResource == null)
                return false;

            var expandedPath = CurrentIconResource.ExpandedFilePath;
            var iconDir = Path.GetDirectoryName(expandedPath);
            return string.Equals(iconDir, FolderPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Whether the icon source file exists
    /// </summary>
    public bool SourceExists => CurrentIconResource?.SourceExists ?? false;

    /// <summary>
    /// Status of this folder's icon configuration
    /// </summary>
    public FolderIconStatus Status
    {
        get
        {
            if (CurrentIconResource == null)
                return FolderIconStatus.NoIcon;
            
            if (IsAlreadyLocal && HasLocalIcon)
                return FolderIconStatus.LocalAndValid;
            
            if (IsAlreadyLocal)
                return FolderIconStatus.LocalButMissing;
            
            if (!SourceExists)
                return FolderIconStatus.ExternalAndBroken;
            
            return FolderIconStatus.ExternalAndValid;
        }
    }

    /// <summary>
    /// The InfoTip (tooltip) from desktop.ini, if any
    /// </summary>
    public string? InfoTip { get; init; }

    /// <summary>
    /// Gets the suggested local icon filename
    /// </summary>
    public string SuggestedLocalIconName => "folder.ico";

    /// <summary>
    /// Gets the full path for the suggested local icon
    /// </summary>
    public string SuggestedLocalIconPath => Path.Combine(FolderPath, SuggestedLocalIconName);
}

/// <summary>
/// Status of a folder's icon configuration
/// </summary>
public enum FolderIconStatus
{
    /// <summary>No custom icon configured</summary>
    NoIcon,
    
    /// <summary>Icon is local and the file exists</summary>
    LocalAndValid,
    
    /// <summary>Icon points to local file but it's missing</summary>
    LocalButMissing,
    
    /// <summary>Icon points to external file that exists</summary>
    ExternalAndValid,
    
    /// <summary>Icon points to external file that doesn't exist</summary>
    ExternalAndBroken
}

