using System.Runtime.InteropServices;
using FolderIconManager.Core.Services;

[DllImport("shell32.dll", CharSet = CharSet.Auto)]
static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

[DllImport("user32.dll")]
static extern bool DestroyIcon(IntPtr hIcon);

var imageresPath = @"C:\Windows\System32\imageres.dll";

Console.WriteLine("=== ICON INDEX DIAGNOSTIC - TESTING NEW PrivateExtractIcons APPROACH ===\n");

// Get total icon count using ExtractIconEx with index -1
var totalIcons = ExtractIconEx(imageresPath, -1, null, null, 0);
Console.WriteLine($"ExtractIconEx reports {totalIcons} icons in imageres.dll");

// Save icons for visual comparison
var testDir = Path.Combine(Path.GetTempPath(), "IconDiagnostic");
if (Directory.Exists(testDir))
    Directory.Delete(testDir, true);
Directory.CreateDirectory(testDir);

Console.WriteLine($"\n--- Extracting icon 266 using NEW PrivateExtractIcons method ---");

using var extractor = new IconExtractor(imageresPath);

// Extract using our new PrivateExtractIcons method
var ourIcon = Path.Combine(testDir, "new_method_icon_266.ico");
try
{
    extractor.SaveIcon(266, ourIcon);
    Console.WriteLine($"SUCCESS! Saved icon 266: {ourIcon}");
    Console.WriteLine($"  File size: {new FileInfo(ourIcon).Length} bytes");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: {ex.Message}");
}

// Also extract a few nearby icons for comparison
foreach (var idx in new[] { 264, 265, 267, 268 })
{
    var path = Path.Combine(testDir, $"icon_{idx}.ico");
    try
    {
        extractor.SaveIcon(idx, path);
        Console.WriteLine($"Extracted icon {idx}: {new FileInfo(path).Length} bytes");
    }
    catch { }
}

Console.WriteLine($"\n*** OPEN FOLDER TO VERIFY: {testDir} ***");
Console.WriteLine("The new_method_icon_266.ico should match the folder icon you see in Windows.");
Console.WriteLine("\nPress Enter to open the folder...");
Console.ReadLine();
System.Diagnostics.Process.Start("explorer.exe", testDir);

