using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace BrowserFork;

sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, string? SetupUrl);

/// <summary>Looks for newer releases on GitHub and installs them with the release's setup.</summary>
static class Updater
{
    public const string Repo = "AlxndrJhn/BrowserFork";
    public const string SetupAsset = "BrowserFork-Setup.exe";

    public static Version Current => Normalize(typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0));

    /// <summary>Only an install made by the setup can be updated by running the next setup; others just get told.</summary>
    public static bool CanSelfUpdate => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", $"BrowserFork/{Current}" }, { "Accept", "application/vnd.github+json" } },
        Timeout = TimeSpan.FromSeconds(30),
    };

    public static async Task<ReleaseInfo?> LatestAsync()
    {
        var json = await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        return ParseRelease(json);
    }

    public static ReleaseInfo? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (ParseTag(tag) is not { } version) return null;
        string? setup = null;
        if (root.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
                if (a.GetProperty("name").GetString() == SetupAsset) setup = a.GetProperty("browser_download_url").GetString();
        return new ReleaseInfo(version, tag, root.GetProperty("html_url").GetString() ?? "", setup);
    }

    /// <summary><c>v1.2.3</c>, <c>1.2</c>, <c>v1.2.3-beta</c> → 1.2.3.0 / 1.2.0.0 / 1.2.3.0.</summary>
    public static Version? ParseTag(string tag) =>
        Version.TryParse(tag.Trim().TrimStart('v', 'V').Split('-', '+')[0], out var v) ? Normalize(v) : null;

    public static bool IsNewer(ReleaseInfo release) => release.Version > Current;

    // Version(1, 2) has Build == -1, which compares as older than 1.2.0.
    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>Downloads the setup and starts it; it asks for admin, closes us and starts the tray again.</summary>
    public static async Task InstallAsync(ReleaseInfo release)
    {
        if (release.SetupUrl == null) throw new InvalidOperationException("The release has no setup to install.");
        var file = Path.Combine(Path.GetTempPath(), $"BrowserFork-Setup-{release.Tag}.exe");
        await using (var src = await Http.GetStreamAsync(release.SetupUrl))
        await using (var dst = File.Create(file))
            await src.CopyToAsync(dst);
        Process.Start(new ProcessStartInfo(file, "/SILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
    }

    public static void OpenPage(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
