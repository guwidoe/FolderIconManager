using System.Text;

namespace FolderIconManager.Tests;

/// <summary>
/// Provides test infrastructure - creates and cleans up test folders
/// </summary>
public class TestFixture : IDisposable
{
    public string TestDataPath { get; }
    public string TestFolder1Path => Path.Combine(TestDataPath, "TestFolder1");
    public string TestFolder2Path => Path.Combine(TestDataPath, "TestFolder2");
    public string TestFolder3Path => Path.Combine(TestDataPath, "TestFolder3");

    public TestFixture()
    {
        // Create a unique test directory for this test run
        TestDataPath = Path.Combine(Path.GetTempPath(), $"FolderIconManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestDataPath);
        
        SetupTestFolders();
    }

    private void SetupTestFolders()
    {
        // Create test folders
        Directory.CreateDirectory(TestFolder1Path);
        Directory.CreateDirectory(TestFolder2Path);
        Directory.CreateDirectory(TestFolder3Path);

        // TestFolder1: External icon pointing to shell32.dll
        WriteDesktopIni(TestFolder1Path, @"C:\Windows\System32\shell32.dll", 4);

        // TestFolder2: External icon pointing to imageres.dll
        WriteDesktopIni(TestFolder2Path, @"C:\Windows\System32\imageres.dll", 3);

        // TestFolder3: Local icon (simulating already fixed, but file missing)
        WriteDesktopIni(TestFolder3Path, "folder.ico", 0);
    }

    public void WriteDesktopIni(string folderPath, string iconPath, int iconIndex)
    {
        var iniPath = Path.Combine(folderPath, "desktop.ini");
        
        // Clear any existing attributes
        if (File.Exists(iniPath))
        {
            File.SetAttributes(iniPath, FileAttributes.Normal);
        }

        // Write INI content with ANSI encoding (Windows requirement)
        var content = $"[.ShellClassInfo]\r\nIconResource={iconPath},{iconIndex}\r\n";
        File.WriteAllText(iniPath, content, Encoding.Default);
    }

    public void ResetTestFolders()
    {
        // Remove any extracted icons and backup manifests
        var filesToRemove = new[]
        {
            Path.Combine(TestFolder1Path, "folder.ico"),
            Path.Combine(TestFolder2Path, "folder.ico"),
            Path.Combine(TestFolder3Path, "folder.ico"),
            Path.Combine(TestFolder1Path, ".folder-icon-backup.json"),
            Path.Combine(TestFolder2Path, ".folder-icon-backup.json"),
            Path.Combine(TestFolder3Path, ".folder-icon-backup.json")
        };

        foreach (var file in filesToRemove)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch { /* Ignore deletion errors */ }
            }
        }

        // Reset desktop.ini files
        SetupTestFolders();
    }

    public void Dispose()
    {
        // Clean up test directory
        try
        {
            if (Directory.Exists(TestDataPath))
            {
                // Remove hidden/system attributes before deleting
                foreach (var file in Directory.GetFiles(TestDataPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch { }
                }

                Directory.Delete(TestDataPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

