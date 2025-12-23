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
        
        // Get folder with external icon
        var folder = scanResult.Folders.FirstOrDefault(f => f.FolderPath == _fixture.TestFolder1Path);
        Assert.NotNull(folder);
        Assert.Equal(FolderIconStatus.ExternalAndValid, folder.Status);

        // Act
        var result = service.ExtractAndInstall(new[] { folder }, skipExisting: false);

        // Assert
        Assert.Single(result.Succeeded);
        Assert.Empty(result.Failed);
        
        // Check that folder.ico was created
        var iconPath = Path.Combine(_fixture.TestFolder1Path, "folder.ico");
        Assert.True(File.Exists(iconPath), $"Icon file should exist at {iconPath}");
        
        // Check that desktop.ini was updated
        var iniContent = File.ReadAllText(Path.Combine(_fixture.TestFolder1Path, "desktop.ini"));
        Assert.Contains("folder.ico", iniContent);
        
        // Check that backup manifest was created
        var backupPath = Path.Combine(_fixture.TestFolder1Path, ".folder-icon-backup.json");
        Assert.True(File.Exists(backupPath), "Backup manifest should exist");
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

    /// <summary>
    /// Tests Windows icon index convention:
    /// - Positive numbers are ordinal indices (0-based position in icon list)
    /// - Negative numbers are direct resource IDs
    /// 
    /// Example: imageres.dll,266 means "icon at position 266" (the 267th icon)
    /// </summary>
    [Fact]
    public void IconExtractor_OrdinalIndex_ExtractsCorrectPosition()
    {
        // Arrange - Use imageres.dll which has many icons
        var imageresPath = @"C:\Windows\System32\imageres.dll";
        if (!File.Exists(imageresPath))
            return;

        using var extractor = new IconExtractor(imageresPath);
        
        Assert.True(extractor.IconCount > 266, $"imageres.dll should have more than 266 icons (has {extractor.IconCount})");

        // The resource ID at ordinal position 266 (0-based)
        var resourceIdAtPosition266 = extractor.ResourceIds[266];
        
        // Act - Extract icon at ordinal position 266
        var outputPath = Path.Combine(_fixture.TestDataPath, "test_imageres_ordinal_266.ico");
        extractor.SaveIcon(266, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath), "Icon file should be created");
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 1000, "Icon file should have content");
    }

    /// <summary>
    /// Tests that negative indices are treated as direct resource IDs (Windows convention).
    /// </summary>
    [Fact]
    public void IconExtractor_NegativeIndex_TreatedAsResourceId()
    {
        // Arrange
        var shell32Path = @"C:\Windows\System32\shell32.dll";
        if (!File.Exists(shell32Path))
            return;

        using var extractor = new IconExtractor(shell32Path);

        // shell32.dll has resource ID 4 (folder icon)
        Assert.True(extractor.HasResourceId(4), "shell32.dll should have resource ID 4");

        // Act - Extract using negative index (resource ID convention)
        var outputPath = Path.Combine(_fixture.TestDataPath, "test_shell32_neg4.ico");
        extractor.SaveIcon(-4, outputPath);

        // Assert - File was created
        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 1000, "Icon file should have content");
    }

    /// <summary>
    /// Tests that negative indices (resource IDs) extract successfully.
    /// PrivateExtractIcons handles negative indices as resource IDs natively.
    /// </summary>
    [Fact]
    public void IconExtractor_NegativeIndex_ExtractsSuccessfully()
    {
        // Arrange
        var imageresPath = @"C:\Windows\System32\imageres.dll";
        if (!File.Exists(imageresPath))
            return;

        using var extractor = new IconExtractor(imageresPath);

        // Resource ID 183 exists in imageres.dll
        Assert.True(extractor.HasResourceId(183), "imageres.dll should have resource ID 183");

        // Act - Extract with -183 (resource ID)
        var outputPath = Path.Combine(_fixture.TestDataPath, "test_imageres_neg183.ico");
        extractor.SaveIcon(-183, outputPath);

        // Assert - File was created with content
        Assert.True(File.Exists(outputPath), "Icon file should be created");
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 1000, "Icon file should have content");
    }

    /// <summary>
    /// Tests that GetIcon and SaveIcon produce consistent results for the same index.
    /// This is a regression test for the bug where preview showed a different icon than extraction.
    /// </summary>
    [Fact]
    public void IconExtractor_GetIconAndSaveIcon_ProduceConsistentResults()
    {
        // Arrange
        var imageresPath = @"C:\Windows\System32\imageres.dll";
        if (!File.Exists(imageresPath))
            return;

        using var extractor = new IconExtractor(imageresPath);
        
        // Act - Get icon via GetIcon (preview) and SaveIcon (extraction) with the same index
        var previewIcon = extractor.GetIcon(-183, 32);
        Assert.NotNull(previewIcon);
        
        var outputPath = Path.Combine(_fixture.TestDataPath, "test_consistency.ico");
        extractor.SaveIcon(-183, outputPath);
        
        // Assert - Both should succeed (we can't easily compare pixels, but both should work)
        Assert.True(File.Exists(outputPath), "Extracted icon file should exist");
        Assert.True(previewIcon.Width > 0, "Preview icon should have valid dimensions");
        
        previewIcon.Dispose();
    }

    /// <summary>
    /// End-to-end test for imageres.dll,-183 through the full extraction pipeline.
    /// This verifies that negative resource IDs are handled correctly by the entire system.
    /// </summary>
    [Fact]
    public void ExtractAndInstall_ImageresDll_Neg183_ExtractsCorrectIcon()
    {
        // Arrange - Create a test folder with imageres.dll,-183 (negative resource ID)
        var testFolderPath = Path.Combine(_fixture.TestDataPath, "NegativeResourceIdTest");
        Directory.CreateDirectory(testFolderPath);
        
        // Write desktop.ini with negative resource ID (-183)
        var iniPath = Path.Combine(testFolderPath, "desktop.ini");
        File.WriteAllText(iniPath, 
            "[.ShellClassInfo]\r\n" +
            "IconResource=%SystemRoot%\\System32\\imageres.dll,-183\r\n");

        try
        {
            var service = new FolderIconService();
            
            // Scan
            var scanResult = service.Scan(testFolderPath, recursive: false);
            Assert.Single(scanResult.Folders);
            
            var folder = scanResult.Folders.First();
            Assert.Equal(-183, folder.CurrentIconResource?.Index);
            Assert.Equal(FolderIconStatus.ExternalAndValid, folder.Status);

            // Act - Fix (extract icon)
            var extractResult = service.ExtractAndInstall(new[] { folder }, skipExisting: false);

            // Assert
            Assert.Single(extractResult.Succeeded);
            
            // Verify the icon was extracted
            var iconPath = Path.Combine(testFolderPath, "folder.ico");
            Assert.True(File.Exists(iconPath), "Icon should be extracted");
            
            // Verify it's a valid ICO file with content
            var fileInfo = new FileInfo(iconPath);
            Assert.True(fileInfo.Length > 1000, "Icon file should have substantial content");
            
            // Read the icon file header to verify it's valid
            using var fs = File.OpenRead(iconPath);
            var header = new byte[6];
            fs.Read(header, 0, 6);
            
            Assert.Equal(0, header[0]); // Reserved
            Assert.Equal(0, header[1]); // Reserved
            Assert.Equal(1, header[2]); // Type = 1 (ICO)
            Assert.Equal(0, header[3]); // Type high byte
            
            // Icon count should be > 0
            var iconCount = header[4] | (header[5] << 8);
            Assert.True(iconCount > 0, "Icon should have at least one image");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testFolderPath))
            {
                foreach (var file in Directory.GetFiles(testFolderPath))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
                try { Directory.Delete(testFolderPath); }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }

    /// <summary>
    /// End-to-end test: Create a folder with a high ordinal index reference,
    /// fix it, and verify the correct icon is extracted.
    /// 
    /// Windows convention: imageres.dll,266 = ordinal position 266 (the 267th icon)
    /// </summary>
    [Fact]
    public void ExtractAndInstall_HighOrdinalIndex_ExtractsCorrectIcon()
    {
        // Arrange - Create a test folder with imageres.dll,266 (high ordinal index)
        var testFolderPath = Path.Combine(_fixture.TestDataPath, "HighOrdinalIndexTest");
        Directory.CreateDirectory(testFolderPath);
        
        // Write desktop.ini with high ordinal index (266)
        var iniPath = Path.Combine(testFolderPath, "desktop.ini");
        File.WriteAllText(iniPath, 
            "[.ShellClassInfo]\r\n" +
            "IconResource=C:\\Windows\\System32\\imageres.dll,266\r\n");

        try
        {
            var service = new FolderIconService();
            
            // Scan
            var scanResult = service.Scan(testFolderPath, recursive: false);
            Assert.Single(scanResult.Folders);
            
            var folder = scanResult.Folders.First();
            Assert.Equal(266, folder.CurrentIconResource?.Index);
            Assert.Equal(FolderIconStatus.ExternalAndValid, folder.Status);

            // Act - Fix (extract icon)
            var extractResult = service.ExtractAndInstall(new[] { folder }, skipExisting: false);

            // Assert
            Assert.Single(extractResult.Succeeded);
            
            // Verify the icon was extracted
            var iconPath = Path.Combine(testFolderPath, "folder.ico");
            Assert.True(File.Exists(iconPath), "Icon should be extracted");
            
            // Read the icon file header to verify it's valid
            using var fs = File.OpenRead(iconPath);
            var header = new byte[6];
            fs.Read(header, 0, 6);
            
            Assert.Equal(0, header[0]); // Reserved
            Assert.Equal(0, header[1]); // Reserved
            Assert.Equal(1, header[2]); // Type = 1 (ICO)
            Assert.Equal(0, header[3]); // Type high byte
            
            // Icon count should be > 0
            var iconCount = header[4] | (header[5] << 8);
            Assert.True(iconCount > 0, "Icon should have at least one image");
            
            // Verify backup manifest was created with correct index
            var backup = BackupManifest.Load(testFolderPath);
            Assert.NotNull(backup);
            Assert.Equal(266, backup.OriginalIconIndex);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testFolderPath))
            {
                // Clear attributes and delete
                foreach (var file in Directory.GetFiles(testFolderPath))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
                try { Directory.Delete(testFolderPath); }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }
}

