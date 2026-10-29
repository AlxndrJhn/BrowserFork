using System.IO;
using System.Windows.Media;
using Microsoft.Win32;

namespace BrowserFork;

sealed record BrowserInfo(string Id, string Name, string ExePath, string BaseArgs, string? PrivateFlag, ImageSource? Icon);

static class BrowserCatalog
{
    static readonly string[] Roots =
    [
        @"SOFTWARE\Clients\StartMenuInternet",
        @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet",
    ];

    // Preferred order; anything else follows alphabetically.
    static readonly string[] Order = ["msedge.exe", "chrome.exe", "firefox.exe"];

    /// <summary>All browsers registered with Windows (HKCU + HKLM), minus ourselves and IE.</summary>
    public static List<BrowserInfo> Discover()
    {
        var self = Environment.ProcessPath ?? "";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<BrowserInfo>();

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var root in Roots)
        {
            using var clients = hive.OpenSubKey(root);
            if (clients == null) continue;
            foreach (var id in clients.GetSubKeyNames())
            {
                if (id.Equals(Registration.AppKey, StringComparison.OrdinalIgnoreCase) ||
                    id.Equals("IEXPLORE.EXE", StringComparison.OrdinalIgnoreCase)) continue;
                using var key = clients.OpenSubKey(id);
                var cmd = key?.OpenSubKey(@"shell\open\command")?.GetValue("") as string;
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                var (exe, args) = SplitCommand(Environment.ExpandEnvironmentVariables(cmd));
                if (!File.Exists(exe) || exe.Equals(self, StringComparison.OrdinalIgnoreCase) || !seen.Add(exe)) continue;

                var name = key!.GetValue("") as string;
                if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(exe);

                list.Add(new BrowserInfo(id, ShortName(name), exe, args, PrivateFlagFor(exe), LoadIcon(key, exe)));
            }
        }

        return list
            .OrderBy(b => Array.IndexOf(Order, Path.GetFileName(b.ExePath).ToLowerInvariant()) is var i && i >= 0 ? i : 99)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static string ShortName(string name) => name switch
    {
        "Mozilla Firefox" => "Firefox",
        "Google Chrome" => "Chrome",
        "Microsoft Edge" => "Edge",
        _ => name,
    };

    static string? PrivateFlagFor(string exe) => Path.GetFileName(exe).ToLowerInvariant() switch
    {
        "msedge.exe" => "--inprivate",
        "firefox.exe" or "librewolf.exe" or "waterfox.exe" => "-private-window",
        "opera.exe" or "launcher.exe" => "--private",
        "chrome.exe" or "brave.exe" or "vivaldi.exe" or "chromium.exe" or "thorium.exe" => "--incognito",
        _ => null,
    };

    static ImageSource? LoadIcon(RegistryKey key, string exe)
    {
        var spec = key.OpenSubKey("DefaultIcon")?.GetValue("") as string;
        if (!string.IsNullOrWhiteSpace(spec))
        {
            spec = Environment.ExpandEnvironmentVariables(spec);
            int comma = spec.LastIndexOf(',');
            string file = spec;
            int index = 0;
            if (comma > 0 && int.TryParse(spec[(comma + 1)..].Trim(), out var idx)) { file = spec[..comma]; index = idx; }
            file = file.Trim().Trim('"');
            if (File.Exists(file) && Native.ExtractIcon(file, index, 64) is { } icon) return icon;
        }
        return Native.ExtractIcon(exe, 0, 64);
    }

    /// <summary>Splits <c>"C:\path\app.exe" --flag</c> into exe and the remaining arguments.</summary>
    public static (string Exe, string Args) SplitCommand(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.StartsWith('"'))
        {
            int end = cmd.IndexOf('"', 1);
            if (end > 0) return (cmd[1..end], StripPlaceholders(cmd[(end + 1)..]));
        }
        int exeEnd = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeEnd > 0) return (cmd[..(exeEnd + 4)], StripPlaceholders(cmd[(exeEnd + 4)..]));
        return (cmd, "");
    }

    // We append the real arguments ourselves.
    static string StripPlaceholders(string args) =>
        args.Replace("\"%1\"", "").Replace("%1", "").Replace("--single-argument", "").Trim();
}
