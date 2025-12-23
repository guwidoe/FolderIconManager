using FolderIconManager.Core.Models;
using FolderIconManager.Core.Native;
using System.Diagnostics;

namespace FolderIconManager.Core.Services;

/// <summary>
/// Main service for managing folder icons - extraction, installation, and configuration
/// </summary>
public class FolderIconService
{
    private readonly DesktopIniScanner _scanner = new();

    /// <summary>
    /// Event raised for progress updates
    /// </summary>
    public event Action<string>? OnProgress;

    /// <summary>
    /// Scans a directory tree for folders with custom icons
    /// </summary>
    public ScanResult Scan(string rootPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        _scanner.OnProgress += msg => OnProgress?.Invoke(msg);
        return _scanner.Scan(rootPath, recursive, cancellationToken);
    }

    /// <summary>
    /// Extracts and installs local icons for the specified folders
    /// </summary>
    public ExtractionResult ExtractAndInstall(
        IEnumerable<FolderIconInfo> folders, 
        bool skipExisting = true,
        CancellationToken cancellationToken = default)
    {
        var result = new ExtractionResult();
        var stopwatch = Stopwatch.StartNew();

        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Skip if already local and valid
                if (skipExisting && folder.Status == FolderIconStatus.LocalAndValid)
                {
                    result.Skipped.Add(folder);
                    OnProgress?.Invoke($"Skipped (already local): {folder.FolderPath}");
                    continue;
                }

                // Skip if source doesn't exist
                if (folder.CurrentIconResource == null || !folder.SourceExists)
                {
                    result.Failed.Add(new ExtractionError
                    {
                        Folder = folder,
                        Message = "Icon source file does not exist"
                    });
                    OnProgress?.Invoke($"Failed (source missing): {folder.FolderPath}");
                    continue;
                }

                // Extract the icon
                var localIconPath = folder.SuggestedLocalIconPath;
                ExtractIcon(folder.CurrentIconResource, localIconPath);

                // Update desktop.ini
                UpdateDesktopIni(folder.FolderPath, localIconPath);

                // Set file attributes
                ApplyAttributes(folder.FolderPath, localIconPath);

                // Notify shell of change
                FileAttributeHelper.NotifyShellOfChange(folder.FolderPath);

                folder.LocalIconPath = localIconPath;
                result.Succeeded.Add(folder);
                OnProgress?.Invoke($"Success: {folder.FolderPath}");
            }
            catch (Exception ex)
            {
                result.Failed.Add(new ExtractionError
                {
                    Folder = folder,
                    Message = ex.Message,
                    Exception = ex
                });
                OnProgress?.Invoke($"Failed: {folder.FolderPath} - {ex.Message}");
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    /// Extracts an icon from a resource to a file
    /// </summary>
    public void ExtractIcon(IconResource resource, string outputPath)
    {
        var sourcePath = resource.ExpandedFilePath;
        var index = resource.Index;

        // Handle negative indices (resource IDs) - convert to zero-based ordinal
        // For now, we treat negative as absolute value for ordinal lookup
        if (index < 0)
            index = Math.Abs(index);

        using var extractor = new IconExtractor(sourcePath);

        if (extractor.IconCount == 0)
            throw new InvalidOperationException($"No icons found in {sourcePath}");

        if (index >= extractor.IconCount)
        {
            // If requested index is out of range, use first icon
            index = 0;
        }

        extractor.SaveIcon(index, outputPath);
    }

    /// <summary>
    /// Updates the desktop.ini file to point to a local icon
    /// </summary>
    public void UpdateDesktopIni(string folderPath, string iconPath)
    {
        var iniPath = Path.Combine(folderPath, "desktop.ini");
        
        // Remove read-only attribute if present (so we can modify)
        if (File.Exists(iniPath))
        {
            FileAttributeHelper.ClearReadOnly(iniPath);
            FileAttributeHelper.ClearHiddenSystem(iniPath);
        }

        var iniFile = new IniFile(iniPath);
        iniFile.EnsureShellClassInfoHeader();

        // Use relative path for the icon (just the filename since it's in the same folder)
        var iconFileName = Path.GetFileName(iconPath);
        iniFile.WriteIconResource(iconFileName, 0);
    }

    /// <summary>
    /// Applies the correct attributes to make the folder icon work
    /// </summary>
    public void ApplyAttributes(string folderPath, string iconPath)
    {
        var iniPath = Path.Combine(folderPath, "desktop.ini");

        // Make desktop.ini hidden and system
        FileAttributeHelper.SetHiddenSystem(iniPath);

        // Make icon file hidden and system
        if (File.Exists(iconPath))
        {
            FileAttributeHelper.SetHiddenSystem(iconPath);
        }

        // Make folder read-only (required for shell to read desktop.ini)
        FileAttributeHelper.SetFolderReadOnly(folderPath);
    }

    /// <summary>
    /// Performs a full fix operation: scan, extract, and install
    /// </summary>
    public (ScanResult scanResult, ExtractionResult extractResult) FixAll(
        string rootPath,
        bool recursive = true,
        bool skipExisting = true,
        CancellationToken cancellationToken = default)
    {
        OnProgress?.Invoke($"Scanning {rootPath}...");
        var scanResult = Scan(rootPath, recursive, cancellationToken);

        OnProgress?.Invoke($"Found {scanResult.FoldersWithIcons} folders with custom icons");
        
        // Only process folders with external icons
        var toProcess = scanResult.ExternalIcons.ToList();
        OnProgress?.Invoke($"Processing {toProcess.Count} folders with external icons...");

        var extractResult = ExtractAndInstall(toProcess, skipExisting, cancellationToken);

        return (scanResult, extractResult);
    }
}

