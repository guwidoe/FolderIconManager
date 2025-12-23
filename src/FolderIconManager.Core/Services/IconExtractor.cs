using System.Drawing;
using System.Runtime.InteropServices;
using FolderIconManager.Core.Native;

namespace FolderIconManager.Core.Services;

/// <summary>
/// Extracts icons from PE files (exe, dll) and ico files with all resolutions preserved
/// </summary>
public class IconExtractor : IDisposable
{
    private readonly string _filePath;
    private readonly IntPtr _hModule;
    private readonly List<int> _iconIds = [];
    private bool _disposed;

    public IconExtractor(string filePath)
    {
        _filePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(filePath));
        
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"File not found: {_filePath}", _filePath);

        // Check if it's an ICO file - handle differently
        if (_filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            _hModule = IntPtr.Zero;
            _iconIds.Add(0); // ICO files have one "icon"
            return;
        }

        // Load the PE file as a data file
        _hModule = NativeMethods.LoadLibraryEx(
            _filePath,
            IntPtr.Zero,
            NativeMethods.LOAD_LIBRARY_AS_DATAFILE | NativeMethods.LOAD_LIBRARY_AS_IMAGE_RESOURCE);

        if (_hModule == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to load file as resource library: {_filePath} (Error: {error})");
        }

        // Enumerate icon group resources
        EnumerateIconGroups();
    }

    /// <summary>
    /// Number of icon groups in the file
    /// </summary>
    public int IconCount => _iconIds.Count;

    /// <summary>
    /// Extracts an icon at the specified index and saves it to a file with all resolutions
    /// </summary>
    public void SaveIcon(int index, string outputPath)
    {
        if (index < 0 || index >= _iconIds.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Icon index must be between 0 and {_iconIds.Count - 1}");

        // Handle ICO files directly - just copy with validation
        if (_hModule == IntPtr.Zero)
        {
            File.Copy(_filePath, outputPath, overwrite: true);
            return;
        }

        var iconId = _iconIds[index];
        SaveIconGroupToFile(iconId, outputPath);
    }

    /// <summary>
    /// Gets an Icon object for the specified index (for preview purposes)
    /// </summary>
    public Icon? GetIcon(int index, int size = 32)
    {
        if (index < 0 || index >= _iconIds.Count)
            return null;

        if (_hModule == IntPtr.Zero)
        {
            // ICO file
            return new Icon(_filePath, size, size);
        }

        var iconId = _iconIds[index];
        return ExtractSingleIcon(iconId, size);
    }

    private void EnumerateIconGroups()
    {
        NativeMethods.EnumResourceNames(
            _hModule,
            NativeMethods.RT_GROUP_ICON,
            (hModule, lpType, lpName, lParam) =>
            {
                // lpName can be an integer ID or a string name
                // We only support integer IDs for simplicity
                if (IsIntResource(lpName))
                {
                    _iconIds.Add((int)lpName);
                }
                return true;
            },
            IntPtr.Zero);
    }

    private static bool IsIntResource(IntPtr value) => ((long)value >> 16) == 0;

    private void SaveIconGroupToFile(int groupId, string outputPath)
    {
        // Find the icon group resource
        var hResInfo = NativeMethods.FindResource(_hModule, new IntPtr(groupId), NativeMethods.RT_GROUP_ICON);
        if (hResInfo == IntPtr.Zero)
            throw new InvalidOperationException($"Icon group {groupId} not found");

        var hResData = NativeMethods.LoadResource(_hModule, hResInfo);
        if (hResData == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to load icon group {groupId}");

        var pResData = NativeMethods.LockResource(hResData);
        if (pResData == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to lock icon group {groupId}");

        var resSize = NativeMethods.SizeofResource(_hModule, hResInfo);

        // Parse the GRPICONDIR structure
        // First 6 bytes: reserved (2), type (2), count (2)
        var reserved = Marshal.ReadInt16(pResData, 0);
        var type = Marshal.ReadInt16(pResData, 2);
        var count = Marshal.ReadInt16(pResData, 4);

        if (type != 1) // Must be icon type
            throw new InvalidOperationException("Resource is not an icon");

        // Read all GRPICONDIRENTRY structures
        var entries = new List<(GRPICONDIRENTRY entry, byte[] data)>();
        var entrySize = Marshal.SizeOf<GRPICONDIRENTRY>();

        for (int i = 0; i < count; i++)
        {
            var entryPtr = IntPtr.Add(pResData, 6 + i * entrySize);
            var entry = Marshal.PtrToStructure<GRPICONDIRENTRY>(entryPtr);

            // Load the individual icon data
            var iconData = LoadIconData(entry.nID);
            if (iconData != null)
            {
                entries.Add((entry, iconData));
            }
        }

        // Write the ICO file
        WriteIcoFile(outputPath, entries);
    }

    private byte[]? LoadIconData(int iconId)
    {
        var hResInfo = NativeMethods.FindResource(_hModule, new IntPtr(iconId), NativeMethods.RT_ICON);
        if (hResInfo == IntPtr.Zero)
            return null;

        var hResData = NativeMethods.LoadResource(_hModule, hResInfo);
        if (hResData == IntPtr.Zero)
            return null;

        var pResData = NativeMethods.LockResource(hResData);
        if (pResData == IntPtr.Zero)
            return null;

        var size = NativeMethods.SizeofResource(_hModule, hResInfo);
        var data = new byte[size];
        Marshal.Copy(pResData, data, 0, (int)size);
        return data;
    }

    private static void WriteIcoFile(string path, List<(GRPICONDIRENTRY entry, byte[] data)> entries)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // Write ICO header
        bw.Write((ushort)0);           // Reserved
        bw.Write((ushort)1);           // Type (1 = icon)
        bw.Write((ushort)entries.Count); // Number of images

        // Calculate offsets - header is 6 bytes, each directory entry is 16 bytes
        var dataOffset = 6 + entries.Count * 16;

        // Write directory entries
        foreach (var (entry, data) in entries)
        {
            bw.Write(entry.bWidth);
            bw.Write(entry.bHeight);
            bw.Write(entry.bColorCount);
            bw.Write((byte)0);              // Reserved
            bw.Write(entry.wPlanes);
            bw.Write(entry.wBitCount);
            bw.Write((uint)data.Length);    // Size of image data
            bw.Write((uint)dataOffset);     // Offset to image data

            dataOffset += data.Length;
        }

        // Write image data
        foreach (var (_, data) in entries)
        {
            bw.Write(data);
        }
    }

    private Icon? ExtractSingleIcon(int groupId, int size)
    {
        var hResInfo = NativeMethods.FindResource(_hModule, new IntPtr(groupId), NativeMethods.RT_GROUP_ICON);
        if (hResInfo == IntPtr.Zero)
            return null;

        var hResData = NativeMethods.LoadResource(_hModule, hResInfo);
        if (hResData == IntPtr.Zero)
            return null;

        var pResData = NativeMethods.LockResource(hResData);
        if (pResData == IntPtr.Zero)
            return null;

        // Find the best matching icon
        var iconId = NativeMethods.LookupIconIdFromDirectoryEx(
            pResData, true, size, size, NativeMethods.LR_DEFAULTCOLOR);

        if (iconId == 0)
            return null;

        // Load the icon data
        var iconData = LoadIconData(iconId);
        if (iconData == null)
            return null;

        // Create icon from resource
        var dataPtr = Marshal.AllocHGlobal(iconData.Length);
        try
        {
            Marshal.Copy(iconData, 0, dataPtr, iconData.Length);
            var hIcon = NativeMethods.CreateIconFromResourceEx(
                dataPtr, (uint)iconData.Length, true, 0x00030000, size, size, NativeMethods.LR_DEFAULTCOLOR);

            if (hIcon == IntPtr.Zero)
                return null;

            // Clone the icon so we can destroy the original handle
            var icon = Icon.FromHandle(hIcon);
            var cloned = (Icon)icon.Clone();
            NativeMethods.DestroyIcon(hIcon);
            return cloned;
        }
        finally
        {
            Marshal.FreeHGlobal(dataPtr);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hModule != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(_hModule);
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~IconExtractor()
    {
        Dispose();
    }
}

