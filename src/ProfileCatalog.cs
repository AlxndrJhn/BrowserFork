using System.IO;
using System.Text.Json;

namespace BrowserFork;

/// <summary>A browser profile. <see cref="Args"/> selects it on the browser's command line.</summary>
sealed record BrowserProfile(string Key, string Name, string Args);

/// <summary>Finds the profiles of Chromium-based browsers (Local State) and Firefox-based ones (profiles.ini).</summary>
static class ProfileCatalog
{
    // Firefox forks keep profiles.ini under %APPDATA%\<this>.
    static readonly Dictionary<string, string> GeckoRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["firefox.exe"] = @"Mozilla\Firefox",
        ["librewolf.exe"] = "librewolf",
        ["waterfox.exe"] = "Waterfox",
    };

    public static List<BrowserProfile> For(string exe, string? localAppData = null, string? appData = null)
    {
        localAppData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        appData ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        try
        {
            if (GeckoRoots.TryGetValue(Path.GetFileName(exe), out var gecko))
                return Gecko(Path.Combine(appData, gecko));
            return ChromiumUserData(exe, localAppData) is { } userData ? Chromium(userData) : [];
        }
        catch { return []; }
    }

    /// <summary>
    /// Chromium browsers install to <c>…\Vendor\Product\Application\x.exe</c> and keep their profiles in
    /// <c>%LOCALAPPDATA%\Vendor\Product\User Data</c> (or <c>%LOCALAPPDATA%\Product\User Data</c>, like Vivaldi).
    /// </summary>
    static string? ChromiumUserData(string exe, string localAppData)
    {
        var appDir = Path.GetDirectoryName(exe);
        if (appDir == null || !Path.GetFileName(appDir).Equals("Application", StringComparison.OrdinalIgnoreCase)) return null;
        var product = Path.GetDirectoryName(appDir);
        var vendor = Path.GetDirectoryName(product);
        if (product == null) return null;

        var candidates = new List<string>();
        if (vendor != null) candidates.Add(Path.Combine(localAppData, Path.GetFileName(vendor), Path.GetFileName(product), "User Data"));
        candidates.Add(Path.Combine(localAppData, Path.GetFileName(product), "User Data"));
        return candidates.FirstOrDefault(d => File.Exists(Path.Combine(d, "Local State")));
    }

    static List<BrowserProfile> Chromium(string userData)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(userData, "Local State")));
        if (!doc.RootElement.TryGetProperty("profile", out var profile) ||
            !profile.TryGetProperty("info_cache", out var cache) || cache.ValueKind != JsonValueKind.Object) return [];

        var order = profile.TryGetProperty("profiles_order", out var o) && o.ValueKind == JsonValueKind.Array
            ? o.EnumerateArray().Select(e => e.GetString()).OfType<string>().ToList()
            : [];

        return cache.EnumerateObject()
            .Where(p => !(p.Value.TryGetProperty("is_ephemeral", out var eph) && eph.ValueKind == JsonValueKind.True))
            .Select(p => new BrowserProfile(
                p.Name,
                p.Value.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name ? name : p.Name,
                $"--profile-directory=\"{p.Name}\""))
            .OrderBy(p => order.IndexOf(p.Key) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(p => p.Key == "Default" ? 0 : 1)
            .ThenBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase) // "Profile 2" before "Profile 10"
            .ToList();
    }

    static List<BrowserProfile> Gecko(string root)
    {
        var ini = Path.Combine(root, "profiles.ini");
        if (!File.Exists(ini)) return [];

        var profiles = new List<BrowserProfile>();
        foreach (var section in ParseIni(File.ReadAllLines(ini)))
        {
            if (!section.Key.StartsWith("Profile", StringComparison.OrdinalIgnoreCase)) continue;
            var values = section.Value;
            if (!values.TryGetValue("Name", out var name) || !values.TryGetValue("Path", out var path)) continue;
            var dir = values.GetValueOrDefault("IsRelative") == "1"
                ? Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))
                : path;
            if (!Directory.Exists(dir)) continue;
            profiles.Add(new BrowserProfile(name, name, $"-P \"{name}\""));
        }
        return profiles;
    }

    static Dictionary<string, Dictionary<string, string>> ParseIni(IEnumerable<string> lines)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
                sections[line[1..^1]] = current = new(StringComparer.OrdinalIgnoreCase);
            else if (current != null && line.IndexOf('=') is var eq && eq > 0)
                current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return sections;
    }
}
