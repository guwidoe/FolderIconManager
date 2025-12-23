using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FolderIconManager.GUI.Helpers;

/// <summary>
/// Helper class for managing window title bar appearance (dark/light mode)
/// </summary>
public static class WindowTitleBarHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>
    /// Applies dark mode or light mode to the window title bar
    /// </summary>
    /// <param name="window">The WPF window</param>
    /// <param name="useDarkMode">True for dark mode, false for light mode</param>
    public static void SetTitleBarTheme(Window window, bool useDarkMode)
    {
        if (window == null) return;

        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;

            int attributeValue = useDarkMode ? 1 : 0;
            
            // Try the newer API first (Windows 10 20H1+)
            int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int));
            
            // If that fails, try the older API (Windows 10 versions before 20H1)
            if (result != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attributeValue, sizeof(int));
            }
        }
        catch
        {
            // Silently fail if DWM APIs are not available (shouldn't happen on Windows 10+)
        }
    }
}

