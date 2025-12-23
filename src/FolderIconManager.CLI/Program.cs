using System.CommandLine;
using FolderIconManager.Core.Models;
using FolderIconManager.Core.Services;

namespace FolderIconManager.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Folder Icon Manager - Extract and manage custom folder icons on Windows")
        {
            Name = "fim"
        };

        // === SCAN COMMAND ===
        var scanCommand = new Command("scan", "Scan for folders with custom icons");
        var scanPathArg = new Argument<string>("path", () => ".", "The directory to scan");
        var recursiveOption = new Option<bool>(["--recursive", "-r"], () => true, "Scan subdirectories");
        var showBrokenOption = new Option<bool>("--broken", "Only show folders with broken icon references");
        var showExternalOption = new Option<bool>("--external", "Only show folders with external icon references");
        
        scanCommand.AddArgument(scanPathArg);
        scanCommand.AddOption(recursiveOption);
        scanCommand.AddOption(showBrokenOption);
        scanCommand.AddOption(showExternalOption);
        
        scanCommand.SetHandler(HandleScan, scanPathArg, recursiveOption, showBrokenOption, showExternalOption);

        // === EXTRACT COMMAND ===
        var extractCommand = new Command("extract", "Extract an icon from a resource file");
        var extractSourceArg = new Argument<string>("source", "The source file (exe, dll, ico) with optional index (e.g., shell32.dll,4)");
        var extractOutputArg = new Argument<string>("output", "The output .ico file path");
        
        extractCommand.AddArgument(extractSourceArg);
        extractCommand.AddArgument(extractOutputArg);
        
        extractCommand.SetHandler(HandleExtract, extractSourceArg, extractOutputArg);

        // === FIX COMMAND ===
        var fixCommand = new Command("fix", "Extract and localize all external folder icons");
        var fixPathArg = new Argument<string>("path", () => ".", "The directory to process");
        var fixRecursiveOption = new Option<bool>(["--recursive", "-r"], () => true, "Process subdirectories");
        var forceOption = new Option<bool>(["--force", "-f"], "Re-extract even if local icon exists");
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be done without making changes");
        
        fixCommand.AddArgument(fixPathArg);
        fixCommand.AddOption(fixRecursiveOption);
        fixCommand.AddOption(forceOption);
        fixCommand.AddOption(dryRunOption);
        
        fixCommand.SetHandler(HandleFix, fixPathArg, fixRecursiveOption, forceOption, dryRunOption);

        // === RESTORE COMMAND ===
        var restoreCommand = new Command("restore", "Restore folder icons to their original external references");
        var restorePathArg = new Argument<string>("path", () => ".", "The directory to restore");
        var restoreRecursiveOption = new Option<bool>(["--recursive", "-r"], () => true, "Process subdirectories");
        
        restoreCommand.AddArgument(restorePathArg);
        restoreCommand.AddOption(restoreRecursiveOption);
        
        restoreCommand.SetHandler(HandleRestore, restorePathArg, restoreRecursiveOption);

        // === UPDATE COMMAND ===
        var updateCommand = new Command("update", "Update local icons from their original sources (if source changed)");
        var updatePathArg = new Argument<string>("path", () => ".", "The directory to update");
        var updateRecursiveOption = new Option<bool>(["--recursive", "-r"], () => true, "Process subdirectories");
        var updateForceOption = new Option<bool>(["--force", "-f"], "Force update even if source hasn't changed");
        
        updateCommand.AddArgument(updatePathArg);
        updateCommand.AddOption(updateRecursiveOption);
        updateCommand.AddOption(updateForceOption);
        
        updateCommand.SetHandler(HandleUpdate, updatePathArg, updateRecursiveOption, updateForceOption);

        // === INFO COMMAND ===
        var infoCommand = new Command("info", "Show detailed information about a folder's icon configuration");
        var infoPathArg = new Argument<string>("path", "The folder to inspect");
        
        infoCommand.AddArgument(infoPathArg);
        infoCommand.SetHandler(HandleInfo, infoPathArg);

        rootCommand.AddCommand(scanCommand);
        rootCommand.AddCommand(extractCommand);
        rootCommand.AddCommand(fixCommand);
        rootCommand.AddCommand(restoreCommand);
        rootCommand.AddCommand(updateCommand);
        rootCommand.AddCommand(infoCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static void HandleScan(string path, bool recursive, bool showBroken, bool showExternal)
    {
        var fullPath = Path.GetFullPath(path);
        Console.WriteLine($"Scanning: {fullPath}");
        Console.WriteLine($"Recursive: {recursive}");
        Console.WriteLine();

        var service = new FolderIconService();
        service.Log.OnLog += entry => Console.WriteLine(entry.ToString());
        
        var result = service.Scan(fullPath, recursive);

        var folders = result.Folders.AsEnumerable();

        if (showBroken)
            folders = result.BrokenIcons;
        else if (showExternal)
            folders = result.ExternalIcons;

        var folderList = folders.ToList();

        if (folderList.Count == 0)
        {
            Console.WriteLine("No matching folders found.");
            return;
        }

        Console.WriteLine($"{"Status",-20} {"Backup",-8} {"Path"}");
        Console.WriteLine(new string('-', 90));

        foreach (var folder in folderList)
        {
            var status = folder.Status switch
            {
                FolderIconStatus.LocalAndValid => "✓ Local",
                FolderIconStatus.LocalButMissing => "✗ Local (missing)",
                FolderIconStatus.ExternalAndValid => "→ External",
                FolderIconStatus.ExternalAndBroken => "✗ Broken",
                _ => "? Unknown"
            };

            var hasBackup = service.HasBackup(folder.FolderPath) ? "Yes" : "-";
            Console.WriteLine($"{status,-20} {hasBackup,-8} {folder.FolderPath}");
        }

        Console.WriteLine();
        Console.WriteLine($"Summary:");
        Console.WriteLine($"  Total folders scanned: {result.TotalFoldersScanned}");
        Console.WriteLine($"  Folders with icons:    {result.FoldersWithIcons}");
        Console.WriteLine($"  Local icons:           {result.LocalIcons.Count()}");
        Console.WriteLine($"  External icons:        {result.ExternalIcons.Count()}");
        Console.WriteLine($"  Broken icons:          {result.BrokenIcons.Count()}");
        Console.WriteLine($"  Scan time:             {result.Duration.TotalSeconds:F2}s");

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Errors ({result.Errors.Count}):");
            foreach (var error in result.Errors.Take(10))
            {
                Console.WriteLine($"  {error.Path}: {error.Message}");
            }
            if (result.Errors.Count > 10)
                Console.WriteLine($"  ... and {result.Errors.Count - 10} more");
        }
    }

    static void HandleExtract(string source, string output)
    {
        try
        {
            var resource = IconResource.Parse(source);
            if (resource == null)
            {
                Console.Error.WriteLine("Invalid source format. Use: path[,index]");
                return;
            }

            Console.WriteLine($"Source: {resource.ExpandedFilePath}");
            Console.WriteLine($"Index:  {resource.Index}");
            Console.WriteLine($"Output: {output}");

            if (!resource.SourceExists)
            {
                Console.Error.WriteLine($"Source file not found: {resource.ExpandedFilePath}");
                return;
            }

            var service = new FolderIconService();
            service.ExtractIcon(resource, output);

            Console.WriteLine();
            Console.WriteLine($"✓ Icon extracted successfully to: {output}");
            
            // Show some info about the extracted icon
            var fileInfo = new FileInfo(output);
            Console.WriteLine($"  Size: {fileInfo.Length:N0} bytes");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    static void HandleFix(string path, bool recursive, bool force, bool dryRun)
    {
        var fullPath = Path.GetFullPath(path);
        Console.WriteLine($"Processing: {fullPath}");
        Console.WriteLine($"Recursive: {recursive}");
        Console.WriteLine($"Force: {force}");
        if (dryRun)
            Console.WriteLine("DRY RUN - No changes will be made");
        Console.WriteLine();

        var service = new FolderIconService();
        service.Log.OnLog += entry => Console.WriteLine(entry.ToString());
        
        // First scan
        Console.WriteLine("Scanning...");
        var scanResult = service.Scan(fullPath, recursive);

        var toProcess = scanResult.ExternalIcons.ToList();
        
        if (toProcess.Count == 0)
        {
            Console.WriteLine("No folders with external icons found.");
            Console.WriteLine($"  Local icons: {scanResult.LocalIcons.Count()}");
            Console.WriteLine($"  Broken icons: {scanResult.BrokenIcons.Count()}");
            return;
        }

        Console.WriteLine($"Found {toProcess.Count} folders with external icons to process.");
        Console.WriteLine();

        if (dryRun)
        {
            Console.WriteLine("Would process:");
            foreach (var folder in toProcess)
            {
                Console.WriteLine($"  {folder.FolderPath}");
                Console.WriteLine($"    From: {folder.CurrentIconResource}");
                Console.WriteLine($"    To:   {folder.SuggestedLocalIconPath}");
            }
            return;
        }

        // Process
        service.OnProgress += msg => Console.WriteLine($"  {msg}");
        var result = service.ExtractAndInstall(toProcess, skipExisting: !force);

        Console.WriteLine();
        Console.WriteLine("Results:");
        Console.WriteLine($"  Succeeded: {result.Succeeded.Count}");
        Console.WriteLine($"  Skipped:   {result.Skipped.Count}");
        Console.WriteLine($"  Failed:    {result.Failed.Count}");
        Console.WriteLine($"  Duration:  {result.Duration.TotalSeconds:F2}s");

        if (result.Failed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Failed:");
            foreach (var error in result.Failed)
            {
                Console.WriteLine($"  {error.Folder.FolderPath}: {error.Message}");
            }
        }
    }

    static void HandleRestore(string path, bool recursive)
    {
        var fullPath = Path.GetFullPath(path);
        Console.WriteLine($"Restoring: {fullPath}");
        Console.WriteLine($"Recursive: {recursive}");
        Console.WriteLine();

        var service = new FolderIconService();
        service.Log.OnLog += entry => Console.WriteLine(entry.ToString());

        var restoredCount = service.RestoreAll(fullPath, recursive);

        Console.WriteLine();
        Console.WriteLine($"Restored {restoredCount} folder(s) to original icon references.");
    }

    static void HandleUpdate(string path, bool recursive, bool force)
    {
        var fullPath = Path.GetFullPath(path);
        Console.WriteLine($"Updating: {fullPath}");
        Console.WriteLine($"Recursive: {recursive}");
        Console.WriteLine($"Force: {force}");
        Console.WriteLine();

        var service = new FolderIconService();
        service.Log.OnLog += entry => Console.WriteLine(entry.ToString());

        var updatedCount = service.UpdateAll(fullPath, recursive, forceUpdate: force);

        Console.WriteLine();
        Console.WriteLine($"Updated {updatedCount} folder(s) from original sources.");
    }

    static void HandleInfo(string path)
    {
        var fullPath = Path.GetFullPath(path);
        
        if (!Directory.Exists(fullPath))
        {
            Console.Error.WriteLine($"Directory not found: {fullPath}");
            return;
        }

        var service = new FolderIconService();
        var scanner = new DesktopIniScanner();
        var iniPath = Path.Combine(fullPath, "desktop.ini");
        
        Console.WriteLine($"Folder: {fullPath}");
        Console.WriteLine();

        if (!File.Exists(iniPath))
        {
            Console.WriteLine("No desktop.ini file found.");
            return;
        }

        var info = scanner.AnalyzeDesktopIni(iniPath);
        
        if (info == null)
        {
            Console.WriteLine("desktop.ini exists but has no icon configuration.");
            return;
        }

        Console.WriteLine("Desktop.ini Configuration:");
        Console.WriteLine($"  Status:      {info.Status}");
        
        if (info.CurrentIconResource != null)
        {
            Console.WriteLine($"  Icon Source: {info.CurrentIconResource.FilePath}");
            Console.WriteLine($"  Icon Index:  {info.CurrentIconResource.Index}");
            Console.WriteLine($"  Resolved:    {info.ResolvedIconPath}");
            Console.WriteLine($"  Exists:      {(info.SourceExists ? "Yes" : "No")}");
        }

        if (info.InfoTip != null)
        {
            Console.WriteLine($"  InfoTip:     {info.InfoTip}");
        }

        Console.WriteLine($"  Is Local:    {(info.IsAlreadyLocal ? "Yes" : "No")}");

        if (info.HasLocalIcon)
        {
            Console.WriteLine($"  Local Icon:  {info.LocalIconPath}");
        }

        // Check for backup
        var backup = service.GetBackup(fullPath);
        Console.WriteLine();
        Console.WriteLine("Backup Information:");
        if (backup != null)
        {
            Console.WriteLine($"  Has Backup:       Yes");
            Console.WriteLine($"  Backup Date:      {backup.BackupDate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Original Source:  {backup.OriginalIconPath},{backup.OriginalIconIndex}");
            Console.WriteLine($"  Source Changed:   {(backup.HasSourceChanged(fullPath) ? "Yes" : "No")}");
        }
        else
        {
            Console.WriteLine($"  Has Backup:       No");
        }

        // Check folder attributes
        Console.WriteLine();
        Console.WriteLine("Attributes:");
        
        var folderAttrs = File.GetAttributes(fullPath);
        Console.WriteLine($"  Folder ReadOnly:    {folderAttrs.HasFlag(FileAttributes.ReadOnly)}");
        
        var iniAttrs = File.GetAttributes(iniPath);
        Console.WriteLine($"  desktop.ini Hidden: {iniAttrs.HasFlag(FileAttributes.Hidden)}");
        Console.WriteLine($"  desktop.ini System: {iniAttrs.HasFlag(FileAttributes.System)}");
    }
}
