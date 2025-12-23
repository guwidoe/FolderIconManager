namespace FolderIconManager.Core.Models;

/// <summary>
/// Result of scanning a folder structure for custom icons
/// </summary>
public class ScanResult
{
    /// <summary>
    /// All folders with custom icons found during the scan
    /// </summary>
    public List<FolderIconInfo> Folders { get; init; } = [];

    /// <summary>
    /// Total number of folders scanned
    /// </summary>
    public int TotalFoldersScanned { get; set; }

    /// <summary>
    /// Number of folders with custom icons
    /// </summary>
    public int FoldersWithIcons => Folders.Count;

    /// <summary>
    /// Folders that already have local icons
    /// </summary>
    public IEnumerable<FolderIconInfo> LocalIcons => 
        Folders.Where(f => f.Status == FolderIconStatus.LocalAndValid);

    /// <summary>
    /// Folders with external icons that could be localized
    /// </summary>
    public IEnumerable<FolderIconInfo> ExternalIcons => 
        Folders.Where(f => f.Status == FolderIconStatus.ExternalAndValid);

    /// <summary>
    /// Folders with broken icon references
    /// </summary>
    public IEnumerable<FolderIconInfo> BrokenIcons => 
        Folders.Where(f => f.Status is FolderIconStatus.ExternalAndBroken or FolderIconStatus.LocalButMissing);

    /// <summary>
    /// Errors encountered during scanning
    /// </summary>
    public List<ScanError> Errors { get; init; } = [];

    /// <summary>
    /// Time taken to complete the scan
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// An error encountered during scanning
/// </summary>
public class ScanError
{
    public required string Path { get; init; }
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

