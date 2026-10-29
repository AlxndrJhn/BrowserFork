using System.IO;
using Xunit;

namespace BrowserFork.Tests;

public sealed class ProfileTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), $"browserfork-test-{Guid.NewGuid():N}");
    string LocalAppData => Path.Combine(_root, "Local");
    string AppData => Path.Combine(_root, "Roaming");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    static BrowserInfo Browser(string id, string exe, string baseArgs = "", string? privateFlag = null, params BrowserProfile[] profiles) =>
        new(id, id, exe, baseArgs, privateFlag, null) { Profiles = profiles };

    [Fact]
    public void Chromium_profiles_come_from_local_state_in_profile_order()
    {
        Write(Path.Combine(LocalAppData, @"Microsoft\Edge\User Data\Local State"), """
            { "profile": {
                "profiles_order": ["Profile 1", "Default"],
                "info_cache": {
                  "Default": { "name": "Personal" },
                  "Profile 1": { "name": "Work" },
                  "Guest": { "name": "Guest", "is_ephemeral": true } } } }
            """);

        var profiles = ProfileCatalog.For(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", LocalAppData, AppData);

        Assert.Equal(["Work", "Personal"], profiles.Select(p => p.Name));
        Assert.Equal("--profile-directory=\"Profile 1\"", profiles[0].Args);
    }

    [Fact]
    public void Without_profiles_order_default_comes_first_then_numeric_order()
    {
        Write(Path.Combine(LocalAppData, @"Vivaldi\User Data\Local State"), """
            { "profile": { "info_cache": {
                "Profile 10": { "name": "Ten" }, "Profile 2": { "name": "Two" }, "Default": { "name": "Main" } } } }
            """);

        var profiles = ProfileCatalog.For(Path.Combine(LocalAppData, @"Vivaldi\Application\vivaldi.exe"), LocalAppData, AppData);

        Assert.Equal(["Main", "Two", "Ten"], profiles.Select(p => p.Name));
    }

    [Fact]
    public void Firefox_profiles_come_from_profiles_ini_and_skip_missing_folders()
    {
        var root = Path.Combine(AppData, @"Mozilla\Firefox");
        Directory.CreateDirectory(Path.Combine(root, @"Profiles\abc.default-release"));
        Write(Path.Combine(root, "profiles.ini"), """
            [Install308046B0AF4A39CB]
            Default=Profiles/abc.default-release

            [Profile0]
            Name=default-release
            IsRelative=1
            Path=Profiles/abc.default-release

            [Profile1]
            Name=gone
            IsRelative=1
            Path=Profiles/xyz.gone

            [General]
            Version=2
            """);

        var profiles = ProfileCatalog.For(@"C:\Program Files\Mozilla Firefox\firefox.exe", LocalAppData, AppData);

        var only = Assert.Single(profiles);
        Assert.Equal("default-release", only.Name);
        Assert.Equal("-P \"default-release\"", only.Args);
    }

    [Fact]
    public void Unknown_browsers_and_broken_files_have_no_profiles()
    {
        Write(Path.Combine(LocalAppData, @"Google\Chrome\User Data\Local State"), "{ not json");

        Assert.Empty(ProfileCatalog.For(@"C:\Program Files\Google\Chrome\Application\chrome.exe", LocalAppData, AppData));
        Assert.Empty(ProfileCatalog.For(@"C:\Tools\opera.exe", LocalAppData, AppData));
    }

    [Fact]
    public void A_browser_gets_one_tile_per_profile_only_when_it_has_several()
    {
        var single = Browser("chrome", "chrome.exe", profiles: new BrowserProfile("Default", "Me", ""));
        var multi = Browser("edge", "msedge.exe", profiles: [new("Default", "Personal", ""), new("Profile 1", "Work", "")]);

        Assert.Equal(["chrome"], Tile.ForBrowser(single).Select(t => t.Id));
        Assert.Null(Tile.ForBrowser(single).Single().ProfileName);
        Assert.Equal(["edge/Default", "edge/Profile 1"], Tile.ForBrowser(multi).Select(t => t.Id));
        Assert.Equal("Work", Tile.ForBrowser(multi).Last().ProfileName);
    }

    [Fact]
    public void Chromium_launch_puts_profile_and_private_flags_before_the_link()
    {
        var edge = Browser("edge", "msedge.exe", "", "--inprivate");
        var work = new BrowserProfile("Profile 1", "Work", "--profile-directory=\"Profile 1\"");

        Assert.Equal("--profile-directory=\"Profile 1\" --inprivate https://x.org",
            BrowserCatalog.LaunchArgs(edge, work, privateWindow: true, "https://x.org"));
        Assert.Equal("https://x.org", BrowserCatalog.LaunchArgs(edge, null, privateWindow: false, "https://x.org"));
    }

    [Fact]
    public void Firefox_drops_osint_only_when_extra_flags_are_added()
    {
        var firefox = Browser("firefox", "firefox.exe", "-osint -url", "-private-window");
        var work = new BrowserProfile("work", "work", "-P \"work\"");

        Assert.Equal("-osint -url https://x.org", BrowserCatalog.LaunchArgs(firefox, null, false, "https://x.org"));
        Assert.Equal("-P \"work\" https://x.org", BrowserCatalog.LaunchArgs(firefox, work, false, "https://x.org"));
        Assert.Equal("-private-window https://x.org", BrowserCatalog.LaunchArgs(firefox, null, true, "https://x.org"));
    }

    [Fact]
    public void History_from_before_profiles_points_at_the_browsers_first_profile()
    {
        var edge = Browser("edge", "msedge.exe", profiles: [new("Default", "Personal", ""), new("Profile 1", "Work", "")]);
        var tiles = Launcher.Tiles([Browser("chrome", "chrome.exe"), edge]);
        var h = new HistoryStore { Last = new HistoryEntry { Choice = "edge", When = DateTime.Now } };

        Assert.Equal(1, h.Suggest(tiles, null, null));
        Assert.Equal("edge/Default", tiles[1].Id);
    }
}
