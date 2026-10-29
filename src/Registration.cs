using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace BrowserFork;

/// <summary>Registers BrowserFork as a browser, per user (no admin needed) or machine-wide (admin).</summary>
static class Registration
{
    public const string AppKey = "BrowserFork";
    public const string UrlProgId = "BrowserFork.URL";
    const string HtmlProgId = "BrowserFork.HTML";
    const string ClientPath = @"Software\Clients\StartMenuInternet\" + AppKey;

    static readonly string[] Protocols = ["http", "https", "ftp"];
    static readonly string[] Extensions = [".htm", ".html", ".shtml", ".xht", ".xhtml"];

    public static void Register(bool machine = false)
    {
        var root = machine ? Registry.LocalMachine : Registry.CurrentUser;
        var exe = Environment.ProcessPath!;
        // Unquoted, like Chrome/Firefox register it.
        var icon = $"{exe},0";
        var open = $"\"{exe}\" \"%1\"";

        using (var classes = root.CreateSubKey(@"Software\Classes"))
        {
            foreach (var (id, desc) in new[] { (UrlProgId, "BrowserFork URL"), (HtmlProgId, "BrowserFork Document") })
            {
                using var k = classes.CreateSubKey(id);
                k.SetValue("", desc);
                if (id == UrlProgId) k.SetValue("URL Protocol", "");
                using (var app = k.CreateSubKey("Application"))
                {
                    app.SetValue("ApplicationName", "BrowserFork");
                    app.SetValue("ApplicationIcon", icon);
                }
                using (var di = k.CreateSubKey("DefaultIcon")) di.SetValue("", icon);
                using (var c = k.CreateSubKey(@"shell\open\command")) c.SetValue("", open);
            }
        }

        using (var client = root.CreateSubKey(ClientPath))
        {
            client.SetValue("", "BrowserFork");
            using (var di = client.CreateSubKey("DefaultIcon")) di.SetValue("", icon);
            using (var c = client.CreateSubKey(@"shell\open\command")) c.SetValue("", $"\"{exe}\"");
            using var cap = client.CreateSubKey("Capabilities");
            cap.SetValue("ApplicationName", "BrowserFork");
            cap.SetValue("ApplicationDescription", "Pick a browser for every link you open.");
            cap.SetValue("ApplicationIcon", icon);
            using (var sm = cap.CreateSubKey("StartMenu")) sm.SetValue("StartMenuInternet", AppKey);
            using (var urls = cap.CreateSubKey("URLAssociations"))
                foreach (var p in Protocols) urls.SetValue(p, UrlProgId);
            using (var files = cap.CreateSubKey("FileAssociations"))
                foreach (var e in Extensions) files.SetValue(e, HtmlProgId);
        }

        using (var reg = root.CreateSubKey(@"Software\RegisteredApplications"))
            reg.SetValue(AppKey, ClientPath + @"\Capabilities");

        CreateShortcut(exe);

        Native.SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Unregister()
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using (var reg = root.OpenSubKey(@"Software\RegisteredApplications", true))
                    reg?.DeleteValue(AppKey, false);
                root.DeleteSubKeyTree(ClientPath, false);
                root.DeleteSubKeyTree(@"Software\Classes\" + UrlProgId, false);
                root.DeleteSubKeyTree(@"Software\Classes\" + HtmlProgId, false);
            }
            catch (UnauthorizedAccessException) { } // machine-wide entries need an elevated --unregister
            catch (System.Security.SecurityException) { }
        }
        try { File.Delete(ShortcutPath); } catch { }
        Autostart = false;
        Native.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Whether the tray icon starts with Windows (per user).</summary>
    public static bool Autostart
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            return run?.GetValue(AppKey) != null;
        }
        set
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) run.SetValue(AppKey, $"\"{Environment.ProcessPath}\" --tray");
            else run.DeleteValue(AppKey, false);
        }
    }

    static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "BrowserFork.lnk");

    static void CreateShortcut(string exe)
    {
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic link = shell.CreateShortcut(ShortcutPath);
            link.TargetPath = exe;
            link.Arguments = "--settings";
            link.Description = "Pick a browser for every link you open.";
            link.Save();
        }
        catch { }
    }

    /// <summary>Windows 11 only lets the user pick the default; open the right Settings page.</summary>
    public static void OpenDefaultAppsSettings()
    {
        try { Process.Start(new ProcessStartInfo($"ms-settings:defaultapps?registeredAppUser={AppKey}") { UseShellExecute = true }); }
        catch { }
    }
}
