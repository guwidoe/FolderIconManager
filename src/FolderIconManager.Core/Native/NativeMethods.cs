using System.Runtime.InteropServices;
using System.Text;

namespace FolderIconManager.Core.Native;

/// <summary>
/// P/Invoke declarations for Windows API functions
/// </summary>
internal static partial class NativeMethods
{
    #region Kernel32 - INI File Operations

    [LibraryImport("kernel32.dll", EntryPoint = "GetPrivateProfileStringW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint GetPrivateProfileString(
        string lpAppName,
        string lpKeyName,
        string lpDefault,
        [Out] char[] lpReturnedString,
        uint nSize,
        string lpFileName);

    [LibraryImport("kernel32.dll", EntryPoint = "WritePrivateProfileStringW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WritePrivateProfileString(
        string lpAppName,
        string lpKeyName,
        string? lpString,
        string lpFileName);

    #endregion

    #region Kernel32 - File Attributes

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileAttributesW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint GetFileAttributes(string lpFileName);

    [LibraryImport("kernel32.dll", EntryPoint = "SetFileAttributesW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetFileAttributes(string lpFileName, uint dwFileAttributes);

    public const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;
    public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    public const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    #endregion

    #region Shell32 - Icon Extraction

    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        IntPtr[] phiconLarge,
        IntPtr[] phiconSmall,
        uint nIcons);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr hIcon);

    [LibraryImport("shell32.dll")]
    public static partial void SHChangeNotify(
        int wEventId,
        uint uFlags,
        IntPtr dwItem1,
        IntPtr dwItem2);

    public const int SHCNE_ASSOCCHANGED = 0x08000000;
    public const int SHCNE_UPDATEDIR = 0x00001000;
    public const int SHCNE_UPDATEITEM = 0x00002000;
    public const uint SHCNF_IDLIST = 0x0000;
    public const uint SHCNF_PATHW = 0x0005;
    public const uint SHCNF_FLUSH = 0x1000;

    #endregion

    #region Shell32 - Icon Resource Loading

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeLibrary(IntPtr hModule);

    [LibraryImport("kernel32.dll", EntryPoint = "EnumResourceNamesW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumResourceNames(
        IntPtr hModule,
        IntPtr lpType,
        EnumResNameDelegate lpEnumFunc,
        IntPtr lParam);

    public delegate bool EnumResNameDelegate(IntPtr hModule, IntPtr lpType, IntPtr lpName, IntPtr lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "FindResourceW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr LockResource(IntPtr hResData);

    [LibraryImport("kernel32.dll")]
    public static partial uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    public const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    public const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

    // Resource types
    public static readonly IntPtr RT_ICON = new(3);
    public static readonly IntPtr RT_GROUP_ICON = new(14);

    #endregion

    #region User32 - Icon Operations

    [LibraryImport("user32.dll")]
    public static partial int LookupIconIdFromDirectoryEx(
        IntPtr presbits,
        [MarshalAs(UnmanagedType.Bool)] bool fIcon,
        int cxDesired,
        int cyDesired,
        uint Flags);

    [LibraryImport("user32.dll")]
    public static partial IntPtr CreateIconFromResourceEx(
        IntPtr presbits,
        uint dwResSize,
        [MarshalAs(UnmanagedType.Bool)] bool fIcon,
        uint dwVer,
        int cxDesired,
        int cyDesired,
        uint Flags);

    public const uint LR_DEFAULTCOLOR = 0x00000000;

    #endregion
}

/// <summary>
/// Icon group directory entry structure
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct GRPICONDIRENTRY
{
    public byte bWidth;
    public byte bHeight;
    public byte bColorCount;
    public byte bReserved;
    public ushort wPlanes;
    public ushort wBitCount;
    public uint dwBytesInRes;
    public ushort nID;
}

/// <summary>
/// Icon directory entry for ICO file
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ICONDIRENTRY
{
    public byte bWidth;
    public byte bHeight;
    public byte bColorCount;
    public byte bReserved;
    public ushort wPlanes;
    public ushort wBitCount;
    public uint dwBytesInRes;
    public uint dwImageOffset;
}

