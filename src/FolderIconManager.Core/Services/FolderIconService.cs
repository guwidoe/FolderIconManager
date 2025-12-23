using FolderIconManager.Core.Models;
using FolderIconManager.Core.Native;
using System.Diagnostics;

namespace FolderIconManager.Core.Services;

/// <summary>
/// Main service for managing folder icons - extraction, installation, and configuration
/// </summary>
public class FolderIconService
{
    private readonly DesktopIniScanner _scanner;
    private readonly LogService _log;

    public FolderIconService(LogService? logService = null)
    {
        _log = logService ?? new LogService();
        _scanner = new DesktopIniScanner(_log);
    }

    /// <summary>
    /// The log service for this instance
    /// </summary>
    public LogService Log => _log;

    /// <summary>
    /// Event raised for progress updates (legacy - prefer using Log service)
    /// </summary>
    public event Action<string>? OnProgress;

    /// <summary>
    /// Scans a directory tree for folders with custom icons
    /// </summary>
    public ScanResult Scan(string rootPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        _log.Info($"Starting scan: {rootPath}");
        _log.Info($"Recursive: {recursive}");
        
        var result = _scanner.Scan(rootPath, recursive, cancellationToken);
        
        _log.Success($"Scan complete: {result.FoldersWithIcons} folders with icons found");
        _log.Info($"  Local: {result.LocalIcons.Count()}, External: {result.ExternalIcons.Count()}, Broken: {result.BrokenIcons.Count()}");
        
        if (result.Errors.Count > 0)
        {
            _log.Warning($"  {result.Errors.Count} errors during scan");
        }
        
        return result;
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
        var folderList = folders.ToList();
        
        _log.Info($"Processing {folderList.Count} folder(s)...");

        for (int i = 0; i < folderList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = folderList[i];
            
            _log.Debug($"[{i + 1}/{folderList.Count}] {folder.FolderPath}");

            try
            {
                // Skip if already local and valid
                if (skipExisting && folder.Status == FolderIconStatus.LocalAndValid)
                {
                    result.Skipped.Add(folder);
                    _log.Info($"Skipped (already local): {folder.FolderPath}");
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
                    _log.Error($"Source missing: {folder.FolderPath}");
                    _log.Debug($"  Source was: {folder.CurrentIconResource?.FilePath ?? "(null)"}");
                    OnProgress?.Invoke($"Failed (source missing): {folder.FolderPath}");
                    continue;
                }

                // Extract the icon
                var localIconPath = folder.SuggestedLocalIconPath;
                var resolvedSourcePath = folder.ResolvedIconPath!;
                _log.Info($"Extracting from: {resolvedSourcePath}");
                _log.Debug($"  Original reference: {folder.CurrentIconResource.FilePath}");
                _log.Debug($"  Index: {folder.CurrentIconResource.Index}");
                _log.Debug($"  Target: {localIconPath}");
                
                ExtractIconFromPath(resolvedSourcePath, folder.CurrentIconResource.Index, localIconPath);
                _log.Debug($"  Icon extracted ({new FileInfo(localIconPath).Length:N0} bytes)");

                // Update desktop.ini
                _log.Debug($"  Updating desktop.ini...");
                UpdateDesktopIni(folder.FolderPath, localIconPath);

                // Set file attributes
                _log.Debug($"  Applying attributes...");
                ApplyAttributes(folder.FolderPath, localIconPath);

                // Notify shell of change
                _log.Debug($"  Notifying shell...");
                FileAttributeHelper.NotifyShellOfChange(folder.FolderPath);

                folder.LocalIconPath = localIconPath;
                result.Succeeded.Add(folder);
                _log.Success($"Fixed: {folder.FolderPath}");
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
                _log.Error($"Failed: {folder.FolderPath}", ex);
                OnProgress?.Invoke($"Failed: {folder.FolderPath} - {ex.Message}");
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        
        _log.Info($"Processing complete in {result.Duration.TotalSeconds:F2}s");
        _log.Info($"  Succeeded: {result.Succeeded.Count}, Skipped: {result.Skipped.Count}, Failed: {result.Failed.Count}");
        
        return result;
    }

    /// <summary>
    /// Extracts an icon from a resource to a file
    /// </summary>
    public void ExtractIcon(IconResource resource, string outputPath)
    {
        var sourcePath = resource.ExpandedFilePath;
        ExtractIconFromPath(sourcePath, resource.Index, outputPath);
    }

    /// <summary>
    /// Extracts an icon from a fully resolved file path
    /// </summary>
    public void ExtractIconFromPath(string sourcePath, int index, string outputPath)
    {
        // Handle negative indices (resource IDs) - convert to zero-based ordinal
        // For now, we treat negative as absolute value for ordinal lookup
        if (index < 0)
            index = Math.Abs(index);

        using var extractor = new IconExtractor(sourcePath);

        if (extractor.IconCount == 0)
            throw new InvalidOperationException($"No icons found in {sourcePath}");

        if (index >= extractor.IconCount)
        {
            _log.Warning($"Icon index {index} out of range (max {extractor.IconCount - 1}), using index 0");
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
        _log.Info($"=== Starting FixAll operation ===");
        _log.Info($"Root: {rootPath}");
        
        var scanResult = Scan(rootPath, recursive, cancellationToken);
        
        // Only process folders with external icons
        var toProcess = scanResult.ExternalIcons.ToList();
        _log.Info($"Found {toProcess.Count} folder(s) with external icons to process");

        var extractResult = ExtractAndInstall(toProcess, skipExisting, cancellationToken);
        
        _log.Info($"=== FixAll complete ===");

        return (scanResult, extractResult);
    }
}
