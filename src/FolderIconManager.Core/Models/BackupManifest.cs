using System.Text.Json;
using System.Text.Json.Serialization;
using FolderIconManager.Core.Native;

namespace FolderIconManager.Core.Models;

/// <summary>
/// Represents the backup information for a folder's icon configuration.
/// Stored as a protected OS file (hidden + system) alongside the desktop.ini,
/// invisible unless "Show protected operating system files" is enabled in Explorer.
/// </summary>
public class BackupManifest
{
    /// <summary>
    /// The manifest file name (hidden alongside desktop.ini)
    /// </summary>
    public const string FileName = ".folder-icon-backup.json";

    /// <summary>
    /// Version of the manifest format
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// When the backup was created
    /// </summary>
    public DateTime BackupDate { get; set; }

    /// <summary>
    /// The original icon resource path (before we modified it)
    /// </summary>
    public string OriginalIconPath { get; set; } = "";

    /// <summary>
    /// The original icon index
    /// </summary>
    public int OriginalIconIndex { get; set; }

    /// <summary>
    /// The original raw value from desktop.ini
    /// </summary>
    public string? OriginalRawValue { get; set; }

    /// <summary>
    /// The local icon file name we created
    /// </summary>
    public string LocalIconName { get; set; } = "folder.ico";

    /// <summary>
    /// Hash of the extracted icon file (to detect if it was manually modified)
    /// </summary>
    public string? IconFileHash { get; set; }

    /// <summary>
    /// Hash of the source file at extraction time (to detect if source was updated)
    /// </summary>
    public string? SourceFileHash { get; set; }

    /// <summary>
    /// Size of the source file at extraction time
    /// </summary>
    public long? SourceFileSize { get; set; }

    /// <summary>
    /// Last modified date of source file at extraction time
    /// </summary>
    public DateTime? SourceFileModified { get; set; }

    /// <summary>
    /// Creates a backup manifest from a folder's current icon configuration
    /// </summary>
    public static BackupManifest Create(FolderIconInfo folder)
    {
        var manifest = new BackupManifest
        {
            BackupDate = DateTime.Now,
            OriginalIconPath = folder.CurrentIconResource?.FilePath ?? "",
            OriginalIconIndex = folder.CurrentIconResource?.Index ?? 0,
            OriginalRawValue = folder.CurrentIconResource?.RawValue,
            LocalIconName = folder.SuggestedLocalIconName
        };

        // Store source file metadata for change detection
        var resolvedPath = folder.ResolvedIconPath;
        if (resolvedPath != null && File.Exists(resolvedPath))
        {
            var fileInfo = new FileInfo(resolvedPath);
            manifest.SourceFileSize = fileInfo.Length;
            manifest.SourceFileModified = fileInfo.LastWriteTimeUtc;
        }

        return manifest;
    }

    /// <summary>
    /// Gets the full path to the manifest file for a folder
    /// </summary>
    public static string GetManifestPath(string folderPath)
    {
        return Path.Combine(folderPath, FileName);
    }

    /// <summary>
    /// Saves the manifest to the folder
    /// </summary>
    public void Save(string folderPath)
    {
        var path = GetManifestPath(folderPath);
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(path, json);

        // Make the manifest a protected OS file (hidden + system), same as desktop.ini
        // This prevents it from showing in Explorer unless "Show protected operating system files" is enabled
        FileAttributeHelper.SetHiddenSystem(path);
    }

    /// <summary>
    /// Loads the manifest from a folder, returns null if not found
    /// </summary>
    public static BackupManifest? Load(string folderPath)
    {
        var path = GetManifestPath(folderPath);
        
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Deserialize<BackupManifest>(json, options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if the source file appears to have been modified since extraction
    /// </summary>
    public bool HasSourceChanged(string folderPath)
    {
        try
        {
            if (string.IsNullOrEmpty(OriginalIconPath))
                return false;

            // Reconstruct the original source path
            var sourcePath = OriginalIconPath;
            if (!Path.IsPathRooted(sourcePath))
            {
                sourcePath = Path.GetFullPath(Path.Combine(folderPath, sourcePath));
            }
            sourcePath = Environment.ExpandEnvironmentVariables(sourcePath);

            if (!File.Exists(sourcePath))
                return false; // Can't check if source is gone

            var fileInfo = new FileInfo(sourcePath);
            
            // Check if size or modified date changed
            if (SourceFileSize.HasValue && fileInfo.Length != SourceFileSize.Value)
                return true;

            if (SourceFileModified.HasValue && fileInfo.LastWriteTimeUtc != SourceFileModified.Value)
                return true;

            return false;
        }
        catch
        {
            // If we can't check, assume no change
            return false;
        }
    }

    /// <summary>
    /// Gets the resolved original source path
    /// </summary>
    public string GetResolvedSourcePath(string folderPath)
    {
        var sourcePath = OriginalIconPath;
        if (!Path.IsPathRooted(sourcePath))
        {
            sourcePath = Path.GetFullPath(Path.Combine(folderPath, sourcePath));
        }
        return Environment.ExpandEnvironmentVariables(sourcePath);
    }

    /// <summary>
    /// Deletes the manifest file if it exists
    /// </summary>
    public static void Delete(string folderPath)
    {
        var path = GetManifestPath(folderPath);
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }
}

