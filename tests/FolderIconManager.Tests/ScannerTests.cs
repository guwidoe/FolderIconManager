using FolderIconManager.Core.Models;
using FolderIconManager.Core.Services;
using Xunit;

namespace FolderIconManager.Tests;

public class ScannerTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public ScannerTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetTestFolders();
    }

    [Fact]
    public void Scan_FindsAllFoldersWithDesktopIni()
    {
        // Arrange
        var service = new FolderIconService();

        // Act
        var result = service.Scan(_fixture.TestDataPath, recursive: true);

        // Assert
        Assert.Equal(3, result.FoldersWithIcons);
        Assert.Contains(result.Folders, f => f.FolderPath == _fixture.TestFolder1Path);
        Assert.Contains(result.Folders, f => f.FolderPath == _fixture.TestFolder2Path);
        Assert.Contains(result.Folders, f => f.FolderPath == _fixture.TestFolder3Path);
    }

    [Fact]
    public void Scan_CorrectlyIdentifiesExternalIcons()
    {
        // Arrange
        var service = new FolderIconService();

        // Act
        var result = service.Scan(_fixture.TestDataPath, recursive: true);

        // Assert
        var externalFolders = result.ExternalIcons.ToList();
        Assert.Equal(2, externalFolders.Count);
        Assert.All(externalFolders, f => Assert.Equal(FolderIconStatus.ExternalAndValid, f.Status));
    }

    [Fact]
    public void Scan_CorrectlyIdentifiesBrokenIcons()
    {
        // Arrange
        var service = new FolderIconService();

        // Act
        var result = service.Scan(_fixture.TestDataPath, recursive: true);

        // Assert - TestFolder3 has a local icon reference but no actual file
        var brokenFolders = result.BrokenIcons.ToList();
        Assert.Single(brokenFolders);
        Assert.Equal(_fixture.TestFolder3Path, brokenFolders[0].FolderPath);
        Assert.Equal(FolderIconStatus.LocalButMissing, brokenFolders[0].Status);
    }

    [Fact]
    public void Scan_NonRecursive_OnlyScansTopLevel()
    {
        // Arrange
        var service = new FolderIconService();

        // Act
        var result = service.Scan(_fixture.TestDataPath, recursive: false);

        // Assert - top level has no desktop.ini
        Assert.Equal(0, result.FoldersWithIcons);
    }
}

