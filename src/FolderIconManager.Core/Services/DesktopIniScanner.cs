using FolderIconManager.Core.Models;
using FolderIconManager.Core.Native;
using System.Diagnostics;

namespace FolderIconManager.Core.Services;

/// <summary>
/// Scans folder structures for desktop.ini files with custom icons
/// </summary>
public class DesktopIniScanner
{
    /// <summary>
    /// Event raised for progress updates during scanning
    /// </summary>
    public event Action<string>? OnProgress;

    /// <summary>
    /// Scans a directory tree for folders with custom icons
    /// </summary>
    public ScanResult Scan(string rootPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        var result = new ScanResult();
        var stopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(rootPath))
        {
            result.Errors.Add(new ScanError
            {
                Path = rootPath,
                Message = "Root directory does not exist"
            });
            return result;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            // Find all desktop.ini files
            var desktopIniFiles = EnumerateDesktopIniFiles(rootPath, searchOption, result, cancellationToken);

            foreach (var iniPath in desktopIniFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.TotalFoldersScanned++;

                try
                {
                    var folderInfo = AnalyzeDesktopIni(iniPath);
                    if (folderInfo != null && folderInfo.CurrentIconResource != null)
                    {
                        result.Folders.Add(folderInfo);
                        OnProgress?.Invoke($"Found: {folderInfo.FolderPath}");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ScanError
                    {
                        Path = iniPath,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ScanError
            {
                Path = rootPath,
                Message = $"Error during scan: {ex.Message}",
                Exception = ex
            });
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    /// Analyzes a single desktop.ini file
    /// </summary>
    public FolderIconInfo? AnalyzeDesktopIni(string iniPath)
    {
        if (!File.Exists(iniPath))
            return null;

        var folderPath = Path.GetDirectoryName(iniPath);
        if (string.IsNullOrEmpty(folderPath))
            return null;

        var iniFile = new IniFile(iniPath);
        var iconResourceStr = iniFile.ReadIconResource();

        if (string.IsNullOrEmpty(iconResourceStr))
            return null;

        var iconResource = IconResource.Parse(iconResourceStr);
        var infoTip = iniFile.ReadInfoTip();

        var info = new FolderIconInfo
        {
            FolderPath = folderPath,
            CurrentIconResource = iconResource,
            InfoTip = infoTip
        };

        // Check if there's already a local icon
        var suggestedPath = info.SuggestedLocalIconPath;
        if (File.Exists(suggestedPath))
        {
            info.LocalIconPath = suggestedPath;
        }

        return info;
    }

    private IEnumerable<string> EnumerateDesktopIniFiles(
        string rootPath, 
        SearchOption searchOption, 
        ScanResult result,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDir = stack.Pop();

            // Check for desktop.ini in this directory
            var iniPath = Path.Combine(currentDir, "desktop.ini");
            if (File.Exists(iniPath))
            {
                yield return iniPath;
            }

            if (searchOption != SearchOption.AllDirectories)
                continue;

            // Add subdirectories to stack
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                {
                    // Skip system directories that typically cause issues
                    var dirName = Path.GetFileName(subDir);
                    if (ShouldSkipDirectory(dirName))
                        continue;

                    stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                result.Errors.Add(new ScanError
                {
                    Path = currentDir,
                    Message = "Access denied"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ScanError
                {
                    Path = currentDir,
                    Message = ex.Message,
                    Exception = ex
                });
            }
        }
    }

    private static bool ShouldSkipDirectory(string name)
    {
        // Skip common system/hidden directories that cause issues
        return name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
    }
}

