# Folder Icon Manager

A Windows utility to manage and persist custom folder icons. Extracts icons from resource files (DLL, EXE) and installs them locally alongside folders, making custom icons portable and resilient to source file changes.

![Screenshot placeholder - run the GUI to see the interface]

## The Problem

Windows custom folder icons are configured via `desktop.ini` files that point to icon resources in external files (like `shell32.dll` or application executables). When these source files move, update, or get deleted, folder icons break.

## The Solution

This tool:
1. **Scans** your folder structure for existing custom folder icons
2. **Extracts** icons from their source files (with all resolutions preserved)
3. **Installs** the icons locally in each folder
4. **Updates** `desktop.ini` to reference the local icon
5. **Applies** correct file attributes (hidden, system, read-only)

The result: self-contained folder icons that survive source file changes.

## Installation

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime (or use the self-contained build)

### Build from Source

```powershell
git clone https://github.com/yourusername/FolderIconManager.git
cd FolderIconManager
dotnet build
```

### Publish Self-Contained Executables

```powershell
# GUI application (recommended)
dotnet publish src/FolderIconManager.GUI -c Release -r win-x64 --self-contained

# Command-line interface
dotnet publish src/FolderIconManager.CLI -c Release -r win-x64 --self-contained
```

## GUI Application

The easiest way to use this tool is through the graphical interface:

1. Run `FolderIconManager.GUI.exe`
2. Click **Browse** to select a folder to scan
3. Click **Scan** to find all folders with custom icons
4. Review the list - icons are color-coded by status:
   - 🟢 **Local** - Already using a local icon file
   - 🟠 **External** - Using icon from DLL/EXE (can be fixed)
   - 🔴 **Broken** - Icon source file is missing
5. Click **Fix All External** to extract and localize all external icons
6. Watch the log panel for detailed progress

## Usage

### Scan for Folder Icons

```powershell
# Scan current directory recursively
fim scan

# Scan specific path
fim scan "D:\Projects" --recursive

# Show only folders with broken icons
fim scan "D:\Projects" --broken

# Show only folders with external (non-local) icons
fim scan "D:\Projects" --external
```

### Extract a Single Icon

```powershell
# Extract icon from a DLL
fim extract "C:\Windows\System32\shell32.dll,4" "output.ico"

# Extract from an EXE
fim extract "C:\Program Files\MyApp\app.exe,0" "app-icon.ico"

# Extract from ICO file (copies with validation)
fim extract "existing.ico" "copy.ico"
```

### Fix All Folder Icons

```powershell
# Preview what would be done
fim fix "D:\Projects" --dry-run

# Fix all external icons in a directory tree
fim fix "D:\Projects" --recursive

# Force re-extraction even if local icon exists
fim fix "D:\Projects" --force
```

### Inspect a Folder

```powershell
fim info "D:\MyFolder"
```

## How It Works

### desktop.ini Structure

Windows uses `desktop.ini` files to customize folder appearance:

```ini
[.ShellClassInfo]
IconResource=shell32.dll,4
InfoTip=My custom folder
ConfirmFileOp=0
```

### What This Tool Does

1. **Finds** `desktop.ini` files with `IconResource` or `IconFile` entries
2. **Extracts** the referenced icon with ALL available resolutions (16x16 to 256x256)
3. **Saves** as `folder.ico` in the same folder
4. **Updates** `desktop.ini` to use the local icon:
   ```ini
   [.ShellClassInfo]
   IconResource=folder.ico,0
   ```
5. **Sets attributes**:
   - `folder.ico`: Hidden + System
   - `desktop.ini`: Hidden + System
   - Parent folder: Read-Only (required for shell to read desktop.ini)
6. **Notifies** Windows Explorer to refresh the icon cache

## Icon Extraction Details

Icons are extracted with all embedded resolutions preserved:
- Common sizes: 16x16, 24x24, 32x32, 48x48, 64x64, 128x128, 256x256
- All color depths: 4-bit, 8-bit, 32-bit (with alpha)
- PNG-compressed high-resolution images (Vista+)

This ensures icons look crisp at any DPI and in any view mode.

## Supported Source Formats

- `.exe` - Windows executables
- `.dll` - Dynamic link libraries
- `.ico` - Icon files (copied directly)
- Any PE file with icon resources

## Project Structure

```
src/
├── FolderIconManager.Core/     # Core library
│   ├── Models/                 # Data models
│   ├── Native/                 # Windows API P/Invoke
│   └── Services/               # Business logic
└── FolderIconManager.CLI/      # Command-line interface
```

## License

MIT License - see [LICENSE.txt](LICENSE.txt)

## Contributing

Contributions welcome! Please open an issue or PR.

## Acknowledgments

- Icon extraction approach inspired by [TsudaKageyu/IconExtractor](https://github.com/TsudaKageyu/IconExtractor)

