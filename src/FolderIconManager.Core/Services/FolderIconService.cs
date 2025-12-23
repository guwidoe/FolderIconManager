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
    public ScanResult Scan(string rootPath, bool recursive = true, int? maxDepth = null, CancellationToken cancellationToken = default)
    {
        _log.Info($"Starting scan: {rootPath}");
        _log.Info($"Recursive: {recursive}, MaxDepth: {maxDepth?.ToString() ?? "unlimited"}");
        
        var result = _scanner.Scan(rootPath, recursive, maxDepth, cancellationToken);
        
        _log.Success($"Scan complete: {result.FoldersWithIcons} folders with icons found");
        _log.Info($"  Local: {result.LocalIcons.Count()}, External: {result.ExternalIcons.Count()}, Broken: {result.BrokenIcons.Count()}");
        
        if (result.Errors.Count > 0)
        {
            _log.Warning($"  {result.Errors.Count} errors during scan");
        }
        
        return result;
    }

    /// <summary>
    /// Gets icon information for a single folder
    /// </summary>
    public FolderIconInfo? GetFolderIconInfo(string folderPath)
    {
        var result = _scanner.Scan(folderPath, recursive: false);
        return result.Folders.FirstOrDefault();
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

                // Create backup manifest BEFORE making changes
                _log.Debug($"  Creating backup manifest...");
                var manifest = BackupManifest.Create(folder);

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

                // Save the backup manifest
                _log.Debug($"  Saving backup manifest...");
                manifest.Save(folder.FolderPath);

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
    /// Restores a folder to its original icon configuration (before fix was applied)
    /// </summary>
    public bool RestoreFromBackup(string folderPath)
    {
        _log.Info($"Restoring from backup: {folderPath}");

        var manifest = BackupManifest.Load(folderPath);
        if (manifest == null)
        {
            _log.Error($"No backup manifest found for: {folderPath}");
            return false;
        }

        try
        {
            var iniPath = Path.Combine(folderPath, "desktop.ini");
            var iconPath = Path.Combine(folderPath, manifest.LocalIconName);

            // Clear attributes so we can modify files
            if (File.Exists(iniPath))
            {
                FileAttributeHelper.ClearReadOnly(iniPath);
                FileAttributeHelper.ClearHiddenSystem(iniPath);
            }

            // Restore original desktop.ini
            _log.Debug($"  Restoring desktop.ini to: {manifest.OriginalIconPath},{manifest.OriginalIconIndex}");
            var iniFile = new IniFile(iniPath);
            iniFile.WriteIconResource(manifest.OriginalIconPath, manifest.OriginalIconIndex);

            // Remove the local icon file
            if (File.Exists(iconPath))
            {
                _log.Debug($"  Removing local icon: {iconPath}");
                FileAttributeHelper.ClearHiddenSystem(iconPath);
                File.Delete(iconPath);
            }

            // Remove the backup manifest
            _log.Debug($"  Removing backup manifest...");
            BackupManifest.Delete(folderPath);

            // Reapply attributes to desktop.ini
            FileAttributeHelper.SetHiddenSystem(iniPath);
            FileAttributeHelper.SetFolderReadOnly(folderPath);

            // Notify shell
            FileAttributeHelper.NotifyShellOfChange(folderPath);

            _log.Success($"Restored: {folderPath}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to restore: {folderPath}", ex);
            return false;
        }
    }

    /// <summary>
    /// Updates the local icon from the original source (re-extracts)
    /// Useful when the source icon has been updated
    /// </summary>
    public bool UpdateFromSource(string folderPath)
    {
        _log.Info($"Updating from source: {folderPath}");

        var manifest = BackupManifest.Load(folderPath);
        if (manifest == null)
        {
            _log.Error($"No backup manifest found for: {folderPath}");
            return false;
        }

        try
        {
            var sourcePath = manifest.GetResolvedSourcePath(folderPath);
            var iconPath = Path.Combine(folderPath, manifest.LocalIconName);

            if (!File.Exists(sourcePath))
            {
                _log.Error($"Source file no longer exists: {sourcePath}");
                return false;
            }

            // Check if source has actually changed
            if (!manifest.HasSourceChanged(folderPath))
            {
                _log.Info($"Source has not changed, skipping update");
                return true;
            }

            _log.Debug($"  Source: {sourcePath}");
            _log.Debug($"  Index: {manifest.OriginalIconIndex}");
            _log.Debug($"  Target: {iconPath}");

            // Clear attributes on existing icon
            if (File.Exists(iconPath))
            {
                FileAttributeHelper.ClearHiddenSystem(iconPath);
            }

            // Re-extract the icon
            ExtractIconFromPath(sourcePath, manifest.OriginalIconIndex, iconPath);
            _log.Debug($"  Icon re-extracted ({new FileInfo(iconPath).Length:N0} bytes)");

            // Update the manifest with new source metadata
            var sourceInfo = new FileInfo(sourcePath);
            manifest.SourceFileSize = sourceInfo.Length;
            manifest.SourceFileModified = sourceInfo.LastWriteTimeUtc;
            manifest.Save(folderPath);

            // Reapply attributes
            FileAttributeHelper.SetHiddenSystem(iconPath);

            // Notify shell
            FileAttributeHelper.NotifyShellOfChange(folderPath);

            _log.Success($"Updated: {folderPath}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to update: {folderPath}", ex);
            return false;
        }
    }

    /// <summary>
    /// Checks if a folder has a backup manifest (was previously fixed)
    /// </summary>
    public bool HasBackup(string folderPath)
    {
        return BackupManifest.Load(folderPath) != null;
    }

    /// <summary>
    /// Gets the backup manifest for a folder, if it exists
    /// </summary>
    public BackupManifest? GetBackup(string folderPath)
    {
        return BackupManifest.Load(folderPath);
    }

    /// <summary>
    /// Checks if the source icon has changed since the local copy was extracted
    /// </summary>
    public bool HasSourceChanged(string folderPath)
    {
        var manifest = BackupManifest.Load(folderPath);
        return manifest?.HasSourceChanged(folderPath) ?? false;
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
    /// Extracts an icon from a fully resolved file path.
    /// 
    /// Index interpretation (Windows desktop.ini convention):
    /// - Negative values (e.g., -183) = resource ID (extracts icon with resource ID 183)
    /// - Positive values (e.g., 5) = ordinal index (extracts the 6th icon)
    /// 
    /// PrivateExtractIcons handles both conventions natively.
    /// </summary>
    public void ExtractIconFromPath(string sourcePath, int index, string outputPath)
    {
        using var extractor = new IconExtractor(sourcePath);

        if (extractor.IconCount == 0)
            throw new InvalidOperationException($"No icons found in {sourcePath}");

        // Pass index directly - PrivateExtractIcons handles both positive (ordinal)
        // and negative (resource ID) indices natively
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
        
        var scanResult = Scan(rootPath, recursive, maxDepth: null, cancellationToken);
        
        // Only process folders with external icons
        var toProcess = scanResult.ExternalIcons.ToList();
        _log.Info($"Found {toProcess.Count} folder(s) with external icons to process");

        var extractResult = ExtractAndInstall(toProcess, skipExisting, cancellationToken);
        
        _log.Info($"=== FixAll complete ===");

        return (scanResult, extractResult);
    }

    /// <summary>
    /// Restores all folders in a path to their original icon configuration
    /// </summary>
    public int RestoreAll(string rootPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        _log.Info($"=== Starting RestoreAll operation ===");
        _log.Info($"Root: {rootPath}");

        var scanResult = Scan(rootPath, recursive, maxDepth: null, cancellationToken);
        var restoredCount = 0;

        foreach (var folder in scanResult.Folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (HasBackup(folder.FolderPath))
            {
                if (RestoreFromBackup(folder.FolderPath))
                {
                    restoredCount++;
                }
            }
        }

        _log.Info($"=== RestoreAll complete: {restoredCount} folder(s) restored ===");
        return restoredCount;
    }

    /// <summary>
    /// Updates all local icons from their original sources (where source has changed)
    /// </summary>
    public int UpdateAll(string rootPath, bool recursive = true, bool forceUpdate = false, CancellationToken cancellationToken = default)
    {
        _log.Info($"=== Starting UpdateAll operation ===");
        _log.Info($"Root: {rootPath}");
        _log.Info($"Force update: {forceUpdate}");

        var scanResult = Scan(rootPath, recursive, maxDepth: null, cancellationToken);
        var updatedCount = 0;

        foreach (var folder in scanResult.LocalIcons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var manifest = GetBackup(folder.FolderPath);
            if (manifest == null)
                continue;

            var hasChanged = manifest.HasSourceChanged(folder.FolderPath);
            if (forceUpdate || hasChanged)
            {
                if (hasChanged)
                    _log.Info($"Source changed: {folder.FolderPath}");
                
                if (UpdateFromSource(folder.FolderPath))
                {
                    updatedCount++;
                }
            }
        }

        _log.Info($"=== UpdateAll complete: {updatedCount} folder(s) updated ===");
        return updatedCount;
    }
}
