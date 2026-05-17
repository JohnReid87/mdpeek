using Microsoft.Win32;

namespace MdPeek.UI.Themes;

/// <summary>
/// Reads the Windows personalisation registry key to determine whether the
/// system is currently running in dark or light app mode.
/// </summary>
internal static class WindowsTheme
{
    private const string PersonalizeKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// Returns <c>true</c> when Windows is configured to use dark app mode,
    /// <c>false</c> when light mode is active. Defaults to <c>true</c> (dark)
    /// if the registry key is absent (pre-1809 builds or unusual environments).
    /// </summary>
    public static bool IsAppDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);

        // AppsUseLightTheme == 0  → dark mode
        // AppsUseLightTheme == 1  → light mode
        if (key?.GetValue("AppsUseLightTheme") is int value)
        {
            return value == 0;
        }

        return true;
    }
}
