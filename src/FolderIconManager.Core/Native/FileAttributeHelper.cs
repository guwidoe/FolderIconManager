namespace FolderIconManager.Core.Native;

/// <summary>
/// Helper for managing file and folder attributes
/// </summary>
public static class FileAttributeHelper
{
    /// <summary>
    /// Sets a file as hidden and system (like desktop.ini should be)
    /// </summary>
    public static bool SetHiddenSystem(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(path);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        var newAttrs = attrs | NativeMethods.FILE_ATTRIBUTE_HIDDEN | NativeMethods.FILE_ATTRIBUTE_SYSTEM;
        return NativeMethods.SetFileAttributes(path, newAttrs);
    }

    /// <summary>
    /// Sets a folder as read-only (required for custom folder icons to work)
    /// </summary>
    public static bool SetFolderReadOnly(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(path);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        // Folders need READ_ONLY flag for shell to check desktop.ini
        var newAttrs = attrs | NativeMethods.FILE_ATTRIBUTE_READONLY;
        return NativeMethods.SetFileAttributes(path, newAttrs);
    }

    /// <summary>
    /// Removes the hidden and system attributes from a file
    /// </summary>
    public static bool ClearHiddenSystem(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(path);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        var newAttrs = attrs & ~(NativeMethods.FILE_ATTRIBUTE_HIDDEN | NativeMethods.FILE_ATTRIBUTE_SYSTEM);
        if (newAttrs == 0)
            newAttrs = NativeMethods.FILE_ATTRIBUTE_NORMAL;
            
        return NativeMethods.SetFileAttributes(path, newAttrs);
    }

    /// <summary>
    /// Checks if a file has the hidden attribute
    /// </summary>
    public static bool IsHidden(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(path);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        return (attrs & NativeMethods.FILE_ATTRIBUTE_HIDDEN) != 0;
    }

    /// <summary>
    /// Checks if a file has the system attribute
    /// </summary>
    public static bool IsSystem(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(path);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        return (attrs & NativeMethods.FILE_ATTRIBUTE_SYSTEM) != 0;
    }

    /// <summary>
    /// Removes the read-only attribute from a file (useful before modifying desktop.ini)
    /// </summary>
    public static bool ClearReadOnly(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(path);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        if ((attrs & NativeMethods.FILE_ATTRIBUTE_READONLY) == 0)
            return true; // Already not read-only

        var newAttrs = attrs & ~NativeMethods.FILE_ATTRIBUTE_READONLY;
        if (newAttrs == 0)
            newAttrs = NativeMethods.FILE_ATTRIBUTE_NORMAL;

        return NativeMethods.SetFileAttributes(path, newAttrs);
    }

    /// <summary>
    /// Notifies the shell that a folder has changed (refreshes icon cache)
    /// </summary>
    public static void NotifyShellOfChange(string folderPath)
    {
        var pathPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(folderPath);
        try
        {
            NativeMethods.SHChangeNotify(
                NativeMethods.SHCNE_UPDATEDIR,
                NativeMethods.SHCNF_PATHW | NativeMethods.SHCNF_FLUSH,
                pathPtr,
                IntPtr.Zero);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(pathPtr);
        }
    }
}

