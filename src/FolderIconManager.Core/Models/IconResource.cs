namespace FolderIconManager.Core.Models;

/// <summary>
/// Represents an icon resource reference (e.g., from desktop.ini)
/// </summary>
public class IconResource
{
    /// <summary>
    /// The path to the file containing the icon (exe, dll, ico, etc.)
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The icon index within the resource file. 
    /// Positive values are ordinal indices, negative values are resource IDs.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// The original raw string from desktop.ini (e.g., "C:\Windows\System32\shell32.dll,4")
    /// </summary>
    public string? RawValue { get; init; }

    /// <summary>
    /// Whether the source file exists
    /// </summary>
    public bool SourceExists => File.Exists(Environment.ExpandEnvironmentVariables(FilePath));

    /// <summary>
    /// Gets the expanded file path (with environment variables resolved)
    /// </summary>
    public string ExpandedFilePath => Environment.ExpandEnvironmentVariables(FilePath);

    /// <summary>
    /// Parses an icon resource string in the format "path,index" or just "path"
    /// </summary>
    public static IconResource? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        
        // Find the last comma to split path and index
        var lastComma = trimmed.LastIndexOf(',');
        
        if (lastComma == -1)
        {
            // No comma - just a path, index defaults to 0
            return new IconResource
            {
                FilePath = trimmed,
                Index = 0,
                RawValue = value
            };
        }

        var path = trimmed[..lastComma];
        var indexStr = trimmed[(lastComma + 1)..];

        if (!int.TryParse(indexStr, out var index))
        {
            // Invalid index, treat the whole thing as a path
            return new IconResource
            {
                FilePath = trimmed,
                Index = 0,
                RawValue = value
            };
        }

        return new IconResource
        {
            FilePath = path,
            Index = index,
            RawValue = value
        };
    }

    public override string ToString() => Index == 0 ? FilePath : $"{FilePath},{Index}";
}

