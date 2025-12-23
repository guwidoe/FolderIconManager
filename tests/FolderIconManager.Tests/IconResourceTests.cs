using FolderIconManager.Core.Models;
using Xunit;

namespace FolderIconManager.Tests;

public class IconResourceTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\shell32.dll,4", @"C:\Windows\System32\shell32.dll", 4)]
    [InlineData(@"C:\Windows\System32\shell32.dll,-4", @"C:\Windows\System32\shell32.dll", -4)]
    [InlineData(@"shell32.dll,0", @"shell32.dll", 0)]
    [InlineData(@"folder.ico", @"folder.ico", 0)]
    [InlineData(@"C:\Path With Spaces\file.dll,10", @"C:\Path With Spaces\file.dll", 10)]
    public void Parse_ValidInput_ReturnsCorrectValues(string input, string expectedPath, int expectedIndex)
    {
        // Act
        var result = IconResource.Parse(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPath, result.FilePath);
        Assert.Equal(expectedIndex, result.Index);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrEmpty_ReturnsNull(string? input)
    {
        // Act
        var result = IconResource.Parse(input);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExpandedFilePath_ExpandsEnvironmentVariables()
    {
        // Arrange
        var resource = IconResource.Parse(@"%SystemRoot%\System32\shell32.dll,4");

        // Act & Assert
        Assert.NotNull(resource);
        Assert.Contains("Windows", resource.ExpandedFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%", resource.ExpandedFilePath);
    }

    [Fact]
    public void SourceExists_ReturnsTrueForExistingFile()
    {
        // Arrange
        var resource = IconResource.Parse(@"C:\Windows\System32\shell32.dll,4");

        // Act & Assert
        Assert.NotNull(resource);
        Assert.True(resource.SourceExists);
    }

    [Fact]
    public void SourceExists_ReturnsFalseForMissingFile()
    {
        // Arrange
        var resource = IconResource.Parse(@"C:\NonExistent\File.dll,4");

        // Act & Assert
        Assert.NotNull(resource);
        Assert.False(resource.SourceExists);
    }
}

