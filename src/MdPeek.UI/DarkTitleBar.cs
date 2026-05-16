using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MdPeek.UI;

/// <summary>
/// Switches the OS-drawn window caption (title bar + min/max/close buttons)
/// to dark mode via the DWM immersive dark mode attribute. Available from
/// Windows 10 build 17763 onward; silently no-ops on older systems.
/// </summary>
internal static class DarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        int useDarkMode = 1;
        _ = DwmSetWindowAttribute(
            hwnd,
            DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref useDarkMode,
            sizeof(int));
    }
}
