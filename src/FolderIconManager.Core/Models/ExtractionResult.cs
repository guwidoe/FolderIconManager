namespace FolderIconManager.Core.Models;

/// <summary>
/// Result of extracting and installing icons for folders
/// </summary>
public class ExtractionResult
{
    /// <summary>
    /// Folders that were successfully processed
    /// </summary>
    public List<FolderIconInfo> Succeeded { get; init; } = [];

    /// <summary>
    /// Folders that failed to process
    /// </summary>
    public List<ExtractionError> Failed { get; init; } = [];

    /// <summary>
    /// Folders that were skipped (already local, etc.)
    /// </summary>
    public List<FolderIconInfo> Skipped { get; init; } = [];

    /// <summary>
    /// Total folders processed
    /// </summary>
    public int TotalProcessed => Succeeded.Count + Failed.Count + Skipped.Count;

    /// <summary>
    /// Time taken to complete the extraction
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// An error encountered during icon extraction
/// </summary>
public class ExtractionError
{
    public required FolderIconInfo Folder { get; init; }
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

