using EzMarkdownViewer.App;

using Microsoft.Win32;

namespace EzMarkdownViewer.UI;

internal sealed class WindowsFileAssociationRegistrar : IFileAssociationRegistrar
{
    // Identifies the executable under HKCU\Software\Classes\Applications. The
    // ".exe" suffix matches what Windows looks for when populating the
    // "Open With" list from the per-user classes hive.
    private const string ApplicationExeName = "ez-markdown-viewer.exe";

    // Per-user ProgID for the markdown association. Namespaced with the app
    // name so we can identify (and later remove) only our own entries when
    // other apps may also have registered ProgIDs for .md.
    private const string ProgId = "ez-markdown-viewer.md";

    private const string FriendlyName = "ez-markdown-viewer";
    private const string ProgIdDescription = "Markdown Document";
    private const string Extension = ".md";

    public void Register()
    {
        var exePath = GetExecutablePath();
        var commandLine = $"\"{exePath}\" \"%1\"";
        var iconReference = $"\"{exePath}\",0";

        // App entry — what populates Windows' "Open With" list.
        using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{ApplicationExeName}"))
        {
            appKey.SetValue("FriendlyAppName", FriendlyName);

            using (var commandKey = appKey.CreateSubKey(@"shell\open\command"))
            {
                commandKey.SetValue(string.Empty, commandLine);
            }

            using (var supportedTypesKey = appKey.CreateSubKey("SupportedTypes"))
            {
                supportedTypesKey.SetValue(Extension, string.Empty);
            }

            using (var iconKey = appKey.CreateSubKey("DefaultIcon"))
            {
                iconKey.SetValue(string.Empty, iconReference);
            }
        }

        // ProgID — required for the app to be selectable as the default
        // .md handler via Windows Settings → Default apps.
        using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progIdKey.SetValue(string.Empty, ProgIdDescription);

            using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
            {
                iconKey.SetValue(string.Empty, iconReference);
            }

            using (var commandKey = progIdKey.CreateSubKey(@"shell\open\command"))
            {
                commandKey.SetValue(string.Empty, commandLine);
            }
        }

        // Advertise our ProgID against the .md extension so Windows offers
        // it in the default-app picker. We only add our value to the existing
        // OpenWithProgids key — never overwrite the (Default) value, since
        // that would hijack any existing system-wide association.
        using (var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}\OpenWithProgids"))
        {
            extensionKey.SetValue(ProgId, string.Empty);
        }
    }

    public void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Applications\{ApplicationExeName}", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);

        // Remove just our ProgID value from the shared OpenWithProgids key
        // so we don't disturb other apps' registrations against .md.
        using var extensionKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}\OpenWithProgids", writable: true);
        if (extensionKey is not null && extensionKey.GetValue(ProgId) is not null)
        {
            extensionKey.DeleteValue(ProgId, throwOnMissingValue: false);
        }
    }

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException(
                "Could not determine the application executable path required for registration.");
        }

        return path;
    }
}
