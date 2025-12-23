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
    /// Checks if a folder has the read-only attribute (required for custom folder icons to work)
    /// </summary>
    public static bool IsFolderReadOnly(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(path);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        return (attrs & NativeMethods.FILE_ATTRIBUTE_READONLY) != 0;
    }

    /// <summary>
    /// Checks if a desktop.ini file has the correct attributes (Hidden + System)
    /// </summary>
    public static bool HasCorrectDesktopIniAttributes(string desktopIniPath)
    {
        if (!File.Exists(desktopIniPath))
            return false;

        var attrs = NativeMethods.GetFileAttributes(desktopIniPath);
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return false;

        var hasHidden = (attrs & NativeMethods.FILE_ATTRIBUTE_HIDDEN) != 0;
        var hasSystem = (attrs & NativeMethods.FILE_ATTRIBUTE_SYSTEM) != 0;

        return hasHidden && hasSystem;
    }

    /// <summary>
    /// Checks if a folder has all the required attributes for custom icons to display:
    /// - Folder has ReadOnly attribute
    /// - desktop.ini has Hidden and System attributes
    /// </summary>
    public static (bool FolderOk, bool DesktopIniOk) CheckFolderIconAttributes(string folderPath)
    {
        var folderOk = IsFolderReadOnly(folderPath);
        var desktopIniPath = Path.Combine(folderPath, "desktop.ini");
        var iniOk = HasCorrectDesktopIniAttributes(desktopIniPath);

        return (folderOk, iniOk);
    }

    /// <summary>
    /// Fixes the attributes on a folder and its desktop.ini to make custom icons display correctly
    /// </summary>
    public static bool FixFolderIconAttributes(string folderPath)
    {
        var desktopIniPath = Path.Combine(folderPath, "desktop.ini");
        
        if (!File.Exists(desktopIniPath))
            return false;

        var folderFixed = SetFolderReadOnly(folderPath);
        var iniFixed = SetHiddenSystem(desktopIniPath);

        if (folderFixed || iniFixed)
        {
            NotifyShellOfChange(folderPath);
        }

        return folderFixed && iniFixed;
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

