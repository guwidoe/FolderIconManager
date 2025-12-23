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
        // First try with both flags, then just DATAFILE if that fails
        _hModule = NativeMethods.LoadLibraryEx(
            _filePath,
            IntPtr.Zero,
            NativeMethods.LOAD_LIBRARY_AS_DATAFILE | NativeMethods.LOAD_LIBRARY_AS_IMAGE_RESOURCE);

        if (_hModule == IntPtr.Zero)
        {
            // Try with just LOAD_LIBRARY_AS_DATAFILE
            _hModule = NativeMethods.LoadLibraryEx(
                _filePath,
                IntPtr.Zero,
                NativeMethods.LOAD_LIBRARY_AS_DATAFILE);
        }

        if (_hModule == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to load file as resource library: {_filePath} (Win32 Error: {error})");
        }

        // Enumerate icon group resources
        EnumerateIconGroups();
    }

    /// <summary>
    /// Number of icon groups in the file
    /// </summary>
    public int IconCount => _iconIds.Count;

    /// <summary>
    /// Gets all resource IDs in the file
    /// </summary>
    public IReadOnlyList<int> ResourceIds => _iconIds;

    /// <summary>
    /// Checks if a resource ID exists in the file
    /// </summary>
    public bool HasResourceId(int resourceId) => _iconIds.Contains(resourceId);

    /// <summary>
    /// Resolves an index to the actual resource ID to use.
    /// 
    /// Windows desktop.ini convention:
    /// - Positive numbers (e.g., 266) = ordinal index (0-based position in icon list)
    /// - Negative numbers (e.g., -266) = direct resource ID (abs value)
    /// </summary>
    private int ResolveToResourceId(int index)
    {
        // Negative numbers are direct resource IDs (Windows convention)
        if (index < 0)
        {
            var resourceId = Math.Abs(index);
            // If this resource ID exists, use it
            if (_iconIds.Contains(resourceId))
            {
                return resourceId;
            }
            // If not found, log warning but still try to use it
            // (in case enumeration missed it)
            return resourceId;
        }

        // Positive numbers are ordinal indices (0-based)
        if (index >= 0 && index < _iconIds.Count)
        {
            return _iconIds[index];
        }

        // Out of range - fall back to first icon
        if (_iconIds.Count > 0)
        {
            return _iconIds[0];
        }

        throw new ArgumentOutOfRangeException(nameof(index), 
            $"No icon found for index {index}. File has {_iconIds.Count} icons.");
    }

    /// <summary>
    /// Extracts an icon and saves it to a file with all resolutions.
    /// 
    /// Index interpretation (Windows convention):
    /// - Positive: ordinal index (e.g., 266 = the 267th icon)
    /// - Negative: resource ID (e.g., -266 = resource with ID 266)
    /// 
    /// Uses PrivateExtractIcons which matches Windows Shell behavior exactly.
    /// </summary>
    public void SaveIcon(int index, string outputPath)
    {
        // Handle ICO files directly - just copy with validation
        if (_filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(_filePath, outputPath, overwrite: true);
            return;
        }

        // Use PrivateExtractIcons to match Windows Shell behavior exactly
        // This extracts at the same index that ExtractIconEx and Windows Shell use
        SaveIconUsingPrivateExtractIcons(index, outputPath);
    }

    /// <summary>
    /// Extracts an icon using PrivateExtractIcons which matches Windows Shell's index interpretation exactly.
    /// Falls back to ExtractIconEx if PrivateExtractIcons fails.
    /// </summary>
    private void SaveIconUsingPrivateExtractIcons(int index, string outputPath)
    {
        // Standard icon sizes to extract for a complete multi-resolution ICO
        // Start with larger sizes first as they're more likely to exist
        int[] sizes = [256, 128, 96, 64, 48, 40, 32, 24, 20, 16];
        
        var entries = new List<(byte width, byte height, byte colorCount, ushort planes, ushort bitCount, byte[] data)>();

        foreach (var size in sizes)
        {
            var icons = new IntPtr[1];
            var ids = new uint[1];
            
            var count = NativeMethods.PrivateExtractIcons(
                _filePath,
                index,
                size,
                size,
                icons,
                ids,
                1,
                0); // LR_DEFAULTCOLOR
            
            if (count > 0 && icons[0] != IntPtr.Zero)
            {
                try
                {
                    var iconData = GetIconDataFromHandle(icons[0], size);
                    if (iconData != null && iconData.Length > 0)
                    {
                        entries.Add((
                            (byte)(size >= 256 ? 0 : size),
                            (byte)(size >= 256 ? 0 : size),
                            0,  // Color count (0 for true color)
                            1,  // Planes
                            32, // Bit count (32-bit ARGB)
                            iconData
                        ));
                    }
                }
                catch
                {
                    // Skip this size if extraction fails
                }
                finally
                {
                    NativeMethods.DestroyIcon(icons[0]);
                }
            }
        }

        // If PrivateExtractIcons didn't work well, fall back to ExtractIconEx
        if (entries.Count == 0)
        {
            // Try ExtractIconEx as fallback
            var largeIcons = new IntPtr[1];
            var smallIcons = new IntPtr[1];
            var extractedCount = NativeMethods.ExtractIconEx(_filePath, index, largeIcons, smallIcons, 1);
            
            if (extractedCount > 0)
            {
                try
                {
                    // Try large icon (32x32)
                    if (largeIcons[0] != IntPtr.Zero)
                    {
                        var iconData = GetIconDataFromHandle(largeIcons[0], 32);
                        if (iconData != null && iconData.Length > 0)
                        {
                            entries.Add((32, 32, 0, 1, 32, iconData));
                        }
                    }
                    
                    // Try small icon (16x16)
                    if (smallIcons[0] != IntPtr.Zero)
                    {
                        var iconData = GetIconDataFromHandle(smallIcons[0], 16);
                        if (iconData != null && iconData.Length > 0)
                        {
                            entries.Add((16, 16, 0, 1, 32, iconData));
                        }
                    }
                }
                finally
                {
                    if (largeIcons[0] != IntPtr.Zero)
                        NativeMethods.DestroyIcon(largeIcons[0]);
                    if (smallIcons[0] != IntPtr.Zero)
                        NativeMethods.DestroyIcon(smallIcons[0]);
                }
            }
        }

        // If still no entries, try using Icon.FromHandle as last resort
        if (entries.Count == 0)
        {
            var largeIcons = new IntPtr[1];
            var extractedCount = NativeMethods.ExtractIconEx(_filePath, index, largeIcons, null, 1);
            
            if (extractedCount > 0 && largeIcons[0] != IntPtr.Zero)
            {
                try
                {
                    using var icon = Icon.FromHandle(largeIcons[0]);
                    using var fs = new FileStream(outputPath, FileMode.Create);
                    icon.Save(fs);
                    return; // Success via Icon.Save
                }
                finally
                {
                    NativeMethods.DestroyIcon(largeIcons[0]);
                }
            }
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException($"Failed to extract icon at index {index} from {_filePath}");
        }

        // Write multi-resolution ICO file
        WriteMultiResolutionIcoFile(outputPath, entries);
    }

    /// <summary>
    /// Gets the raw icon data from an icon handle using GetIconInfo and GetDIBits
    /// </summary>
    private static byte[]? GetIconDataFromHandle(IntPtr hIcon, int size)
    {
        // Get icon info
        if (!GetIconInfo(hIcon, out var iconInfo))
            return null;

        try
        {
            // Create a device context
            var hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return null;

            try
            {
                // Setup BITMAPINFOHEADER for the DIB
                var bmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = size,
                    biHeight = size * 2, // Include both XOR and AND masks
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0, // BI_RGB
                    biSizeImage = 0,
                    biXPelsPerMeter = 0,
                    biYPelsPerMeter = 0,
                    biClrUsed = 0,
                    biClrImportant = 0
                };

                // Get the color bitmap data
                var colorData = new byte[size * size * 4];
                var colorBmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = size,
                    biHeight = -size, // Top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0
                };

                var colorHandle = GCHandle.Alloc(colorData, GCHandleType.Pinned);
                try
                {
                    GetDIBits(hdc, iconInfo.hbmColor, 0, (uint)size, 
                        colorHandle.AddrOfPinnedObject(), ref colorBmi, 0);
                }
                finally
                {
                    colorHandle.Free();
                }

                // Get the mask bitmap data
                var maskData = new byte[size * size / 8]; // 1-bit mask
                var maskBmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = size,
                    biHeight = -size,
                    biPlanes = 1,
                    biBitCount = 1,
                    biCompression = 0
                };

                var maskHandle = GCHandle.Alloc(maskData, GCHandleType.Pinned);
                try
                {
                    GetDIBits(hdc, iconInfo.hbmMask, 0, (uint)size,
                        maskHandle.AddrOfPinnedObject(), ref maskBmi, 0);
                }
                finally
                {
                    maskHandle.Free();
                }

                // Build ICO image data: BITMAPINFOHEADER + XOR mask (color) + AND mask
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // Write BITMAPINFOHEADER (height is doubled for XOR+AND)
                var icoHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = size,
                    biHeight = size * 2, // XOR + AND masks
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                    biSizeImage = (uint)(colorData.Length + (size * size / 8)),
                    biXPelsPerMeter = 0,
                    biYPelsPerMeter = 0,
                    biClrUsed = 0,
                    biClrImportant = 0
                };

                // Write header
                bw.Write(icoHeader.biSize);
                bw.Write(icoHeader.biWidth);
                bw.Write(icoHeader.biHeight);
                bw.Write(icoHeader.biPlanes);
                bw.Write(icoHeader.biBitCount);
                bw.Write(icoHeader.biCompression);
                bw.Write(icoHeader.biSizeImage);
                bw.Write(icoHeader.biXPelsPerMeter);
                bw.Write(icoHeader.biYPelsPerMeter);
                bw.Write(icoHeader.biClrUsed);
                bw.Write(icoHeader.biClrImportant);

                // Write color data (XOR mask) - flip vertically for ICO format
                for (int y = size - 1; y >= 0; y--)
                {
                    bw.Write(colorData, y * size * 4, size * 4);
                }

                // Write AND mask (1-bit, rows padded to 4 bytes)
                int andRowBytes = ((size + 31) / 32) * 4;
                var andMask = new byte[andRowBytes * size];
                // AND mask is all zeros for fully opaque 32-bit icons with alpha
                bw.Write(andMask);

                return ms.ToArray();
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero)
                DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero)
                DeleteObject(iconInfo.hbmMask);
        }
    }

    private static void WriteMultiResolutionIcoFile(string path, List<(byte width, byte height, byte colorCount, ushort planes, ushort bitCount, byte[] data)> entries)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // Write ICO header
        bw.Write((ushort)0);           // Reserved
        bw.Write((ushort)1);           // Type (1 = icon)
        bw.Write((ushort)entries.Count); // Number of images

        // Calculate data offsets
        var dataOffset = 6 + entries.Count * 16;

        // Write directory entries
        foreach (var (width, height, colorCount, planes, bitCount, data) in entries)
        {
            bw.Write(width);
            bw.Write(height);
            bw.Write(colorCount);
            bw.Write((byte)0);           // Reserved
            bw.Write(planes);
            bw.Write(bitCount);
            bw.Write((uint)data.Length); // Size of image data
            bw.Write((uint)dataOffset);  // Offset to image data

            dataOffset += data.Length;
        }

        // Write image data
        foreach (var (_, _, _, _, _, data) in entries)
        {
            bw.Write(data);
        }
    }

    #region Native methods for icon data extraction

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        IntPtr lpvBits, ref BITMAPINFOHEADER lpbmi, uint uUsage);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    #endregion

    /// <summary>
    /// Gets an Icon object for preview purposes.
    /// Uses PrivateExtractIcons to match Windows Shell behavior exactly.
    /// </summary>
    public Icon? GetIcon(int index, int size = 32)
    {
        if (_filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            // ICO file
            try
            {
                return new Icon(_filePath, size, size);
            }
            catch
            {
                return null;
            }
        }

        // Use PrivateExtractIcons to get the same icon that Windows Shell uses
        var icons = new IntPtr[1];
        var ids = new uint[1];
        
        var count = NativeMethods.PrivateExtractIcons(
            _filePath,
            index,
            size,
            size,
            icons,
            ids,
            1,
            0); // LR_DEFAULTCOLOR
        
        if (count > 0 && icons[0] != IntPtr.Zero)
        {
            try
            {
                var icon = Icon.FromHandle(icons[0]);
                var cloned = (Icon)icon.Clone();
                return cloned;
            }
            finally
            {
                NativeMethods.DestroyIcon(icons[0]);
            }
        }

        return null;
    }

    private void EnumerateIconGroups()
    {
        // Keep a reference to the delegate to prevent GC
        NativeMethods.EnumResNameDelegate callback = (hModule, lpType, lpName, lParam) =>
        {
            // lpName can be an integer ID or a string name
            // We only support integer IDs for simplicity
            if (IsIntResource(lpName))
            {
                _iconIds.Add((int)lpName);
            }
            return true;
        };
        
        var result = NativeMethods.EnumResourceNames(
            _hModule,
            NativeMethods.RT_GROUP_ICON,
            callback,
            IntPtr.Zero);
        
        // EnumResourceNames returns false if the resource type doesn't exist (ERROR_RESOURCE_TYPE_NOT_FOUND = 1813)
        // This is not necessarily an error - the file just has no icons
        if (!result)
        {
            var error = Marshal.GetLastWin32Error();
            // 1813 = ERROR_RESOURCE_TYPE_NOT_FOUND - no icons in file
            // 0 = success (enumeration completed, possibly with no items)
            if (error != 0 && error != 1813)
            {
                throw new InvalidOperationException($"Failed to enumerate icon resources (Win32 Error: {error})");
            }
        }
        
        // Keep the callback alive until we're done
        GC.KeepAlive(callback);
        
        // CRITICAL: Sort resource IDs numerically to match Windows Shell behavior.
        // Windows uses ordinal indices based on numerically sorted resource IDs,
        // but EnumResourceNames returns them in file storage order (which may differ).
        // Without this sort, icon index 266 might extract the wrong icon.
        _iconIds.Sort();
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

