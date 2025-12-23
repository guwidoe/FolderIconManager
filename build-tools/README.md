# Build Tools

This directory contains build-time utilities for the FolderIconManager project.

## ConvertSvgToIco.ps1

Automatically converts `AppIcon.svg` to `AppIcon.ico` during the build process.

### How It Works

The script is automatically invoked by MSBuild before each build of the GUI project. It:

1. Checks if the SVG file has been modified since the last conversion
2. If conversion is needed, tries multiple methods in order of quality:
   - **Inkscape** (best quality) - if installed
   - **ImageMagick** (good quality) - if installed
   - **.NET fallback** (basic quality) - always available
3. Generates a multi-resolution ICO file with sizes: 16x16, 32x32, 48x48, 256x256

### Installing Icon Conversion Tools (Optional)

For the best icon quality, install one of these tools:

#### Inkscape (Recommended)
- Download: https://inkscape.org/
- Best for: Vector graphics, crisp rendering at all sizes
- After installation, ensure `inkscape.exe` is in your PATH

#### ImageMagick
- Download: https://imagemagick.org/
- Good for: Reliable conversion with good quality
- After installation, ensure `magick.exe` or `convert.exe` is in your PATH

If neither tool is installed, the script will use a built-in .NET method that creates a simple fallback icon based on the SVG's color scheme.

### Manual Conversion

You can manually trigger the conversion:

```powershell
.\build-tools\ConvertSvgToIco.ps1 -SvgPath "AppIcon.svg" -IcoPath "src\FolderIconManager.GUI\AppIcon.ico"
```

### Integration with Build Process

The conversion is integrated into `FolderIconManager.GUI.csproj` via a custom MSBuild target:

```xml
<Target Name="ConvertSvgToIco" BeforeTargets="BeforeBuild">
  <!-- Automatically converts SVG to ICO before each build -->
</Target>
```

The generated `AppIcon.ico` is:
- Used as the application icon (embedded in the .exe)
- Copied to the output directory (`artifacts/bin/FolderIconManager.GUI/...`)
- Loaded by the WPF window for the title bar icon

Note: Build output is centralized in the `artifacts/` folder at the repository root (configured via `Directory.Build.props`).

### Modifying the Icon

1. Edit `AppIcon.svg` with any SVG editor (Inkscape, Adobe Illustrator, etc.)
2. Save your changes
3. Run `dotnet build` — the icon will be automatically updated
4. The new icon will appear in:
   - File Explorer (the .exe file icon)
   - The application window title bar
   - The Windows taskbar when running

### Troubleshooting

**Problem:** "Neither Inkscape nor ImageMagick found" warning

**Solution:** This is not an error! The script will use the .NET fallback method. For better quality, install Inkscape or ImageMagick.

---

**Problem:** ICO file is not updating after changing the SVG

**Solution:** 
1. Delete `src\FolderIconManager.GUI\AppIcon.ico`
2. Run `dotnet build` again
3. The SVG will be reconverted

---

**Problem:** Script fails with permission error

**Solution:** Ensure you have write permissions to the `src\FolderIconManager.GUI\` directory.

