namespace FolderIconManager.Core.Native;

/// <summary>
/// Wrapper for Windows INI file operations using native APIs
/// </summary>
public class IniFile
{
    private readonly string _path;

    public IniFile(string path)
    {
        _path = Path.GetFullPath(path);
    }

    public string Path => _path;
    public bool Exists => File.Exists(_path);

    /// <summary>
    /// Reads a string value from the INI file
    /// </summary>
    public string? ReadString(string section, string key, string? defaultValue = null)
    {
        var buffer = new char[2048];
        var chars = NativeMethods.GetPrivateProfileString(
            section, key, defaultValue ?? string.Empty, buffer, (uint)buffer.Length, _path);

        if (chars == 0)
            return defaultValue;

        return new string(buffer, 0, (int)chars);
    }

    /// <summary>
    /// Writes a string value to the INI file
    /// </summary>
    public bool WriteString(string section, string key, string? value)
    {
        return NativeMethods.WritePrivateProfileString(section, key, value, _path);
    }

    /// <summary>
    /// Reads the IconResource value from desktop.ini [.ShellClassInfo] section
    /// </summary>
    public string? ReadIconResource()
    {
        // Try IconResource first (modern)
        var iconResource = ReadString(".ShellClassInfo", "IconResource");
        if (!string.IsNullOrEmpty(iconResource))
            return iconResource;

        // Fall back to IconFile + IconIndex (legacy)
        var iconFile = ReadString(".ShellClassInfo", "IconFile");
        if (!string.IsNullOrEmpty(iconFile))
        {
            var iconIndex = ReadString(".ShellClassInfo", "IconIndex", "0");
            return $"{iconFile},{iconIndex}";
        }

        return null;
    }

    /// <summary>
    /// Writes the IconResource value to desktop.ini [.ShellClassInfo] section
    /// </summary>
    public bool WriteIconResource(string iconPath, int index = 0)
    {
        // Use IconResource format (modern)
        var value = index == 0 ? iconPath : $"{iconPath},{index}";
        
        // Clear legacy entries if they exist
        WriteString(".ShellClassInfo", "IconFile", null);
        WriteString(".ShellClassInfo", "IconIndex", null);
        
        return WriteString(".ShellClassInfo", "IconResource", value);
    }

    /// <summary>
    /// Reads the InfoTip value from desktop.ini
    /// </summary>
    public string? ReadInfoTip()
    {
        return ReadString(".ShellClassInfo", "InfoTip");
    }

    /// <summary>
    /// Ensures the desktop.ini has the required header for folder customization
    /// </summary>
    public void EnsureShellClassInfoHeader()
    {
        // Writing any value to the section ensures it exists
        // We'll write ConfirmFileOp=0 which prevents "you are customizing a system folder" warnings
        var existing = ReadString(".ShellClassInfo", "ConfirmFileOp");
        if (string.IsNullOrEmpty(existing))
        {
            WriteString(".ShellClassInfo", "ConfirmFileOp", "0");
        }
    }
}

