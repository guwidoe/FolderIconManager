using FolderIconManager.Core.Models;
using FolderIconManager.Core.Services;
using Xunit;

namespace FolderIconManager.Tests;

public class ExtractionTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public ExtractionTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ExtractIcon_FromShell32_CreatesValidIcoFile()
    {
        // Arrange
        var service = new FolderIconService();
        var resource = IconResource.Parse(@"C:\Windows\System32\shell32.dll,4")!;
        var outputPath = Path.Combine(_fixture.TestDataPath, "test_extract.ico");

        // Act
        service.ExtractIcon(resource, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 10000, "Icon file should be larger than 10KB (contains multiple resolutions)");

        // Verify it's a valid ICO file (starts with 00 00 01 00)
        var header = new byte[4];
        using (var fs = File.OpenRead(outputPath))
        {
            fs.Read(header, 0, 4);
        }
        Assert.Equal(0, header[0]); // Reserved
        Assert.Equal(0, header[1]); // Reserved
        Assert.Equal(1, header[2]); // Type (1 = ICO)
        Assert.Equal(0, header[3]); // Type high byte
    }

    [Fact]
    public void ExtractAndInstall_UpdatesDesktopIniCorrectly()
    {
        // Arrange
        _fixture.ResetTestFolders();
        var service = new FolderIconService();
        var scanResult = service.Scan(_fixture.TestDataPath, recursive: true);
        var folder = scanResult.Folders.First(f => f.FolderPath == _fixture.TestFolder1Path);

        // Act
        var result = service.ExtractAndInstall(new[] { folder }, skipExisting: true);

        // Assert
        Assert.Single(result.Succeeded);
        
        // Check that folder.ico was created
        var iconPath = Path.Combine(_fixture.TestFolder1Path, "folder.ico");
        Assert.True(File.Exists(iconPath));
        
        // Check that desktop.ini was updated
        var iniContent = File.ReadAllText(Path.Combine(_fixture.TestFolder1Path, "desktop.ini"));
        Assert.Contains("folder.ico", iniContent);
    }

    [Fact]
    public void ExtractAndInstall_AfterFix_RescanShowsLocal()
    {
        // Arrange
        _fixture.ResetTestFolders();
        var service = new FolderIconService();

        // Act - Fix
        var (scanResult, extractResult) = service.FixAll(_fixture.TestDataPath, recursive: true);

        // Rescan
        var rescanResult = service.Scan(_fixture.TestDataPath, recursive: true);

        // Assert
        Assert.Equal(2, extractResult.Succeeded.Count);
        Assert.Equal(2, rescanResult.LocalIcons.Count());
        Assert.All(rescanResult.LocalIcons, f => Assert.Equal(FolderIconStatus.LocalAndValid, f.Status));
    }

    [Fact]
    public void ExtractAndInstall_SkipExisting_DoesNotReExtract()
    {
        // Arrange
        _fixture.ResetTestFolders();
        var service = new FolderIconService();
        
        // First extraction
        service.FixAll(_fixture.TestDataPath, recursive: true);
        
        // Rescan and try to fix again
        var scanResult = service.Scan(_fixture.TestDataPath, recursive: true);
        var localFolders = scanResult.LocalIcons.ToList();

        // Act - try to fix already-local icons with skipExisting=true
        var result = service.ExtractAndInstall(localFolders, skipExisting: true);

        // Assert
        Assert.Equal(2, result.Skipped.Count);
        Assert.Empty(result.Succeeded);
    }
}

