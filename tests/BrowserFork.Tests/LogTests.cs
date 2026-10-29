using System.IO;
using Xunit;

namespace BrowserFork.Tests;

public class LogTests
{
    const string Profile = @"C:\Users\jane.doe";
    static readonly string[] Names = ["jane.doe", "JANES-PC"];

    static string Scrub(string text) => Anonymizer.Scrub(text, Profile, Names);

    [Theory]
    [InlineData("https://github.com/org/repo/issues?q=secret#top", "https://<host>/<path>?<query>#<fragment>")]
    [InlineData("\"http://intranet:8080/\"", "\"http://<host>:<port>\"")]
    [InlineData("microsoft-edge:https://example.com/x", "microsoft-edge:https://<host>/<path>")]
    public void Links_keep_their_shape_but_not_their_content(string text, string expected)
    {
        Assert.Equal(expected, Scrub(text));
    }

    [Fact]
    public void Scrubbing_twice_changes_nothing()
    {
        var once = Scrub("Link \"https://github.com/a?b\" from outlook.exe");
        Assert.Equal(once, Scrub(once));
    }

    [Fact]
    public void Removes_email_addresses_user_names_and_personal_paths()
    {
        var text = Scrub(@"mailto:jane@example.org in C:\Users\jane.doe\AppData\x.json, D:\Users\bob\y, by Jane.Doe on janes-pc");

        Assert.Equal(@"mailto:<email> in %USERPROFILE%\AppData\x.json, D:\Users\<user>\y, by <name> on <name>", text);
    }

    [Fact]
    public void Names_are_only_replaced_as_whole_words()
    {
        Assert.Equal("Installed by <name>", Anonymizer.Scrub("Installed by al", null, ["al"]));
        Assert.Equal("https://<host>", Anonymizer.Scrub("https://host.example", null, ["host"]));
    }

    [Fact]
    public void Profiles_are_logged_by_position_not_by_name()
    {
        var firefox = new BrowserInfo("firefox", "Firefox", "firefox.exe", "", "-private-window", null)
        {
            Profiles = [new("default-release", "default-release", ""), new("Jane", "Jane", "-P \"Jane\"")],
        };
        var tiles = Launcher.Tiles([firefox]);

        Assert.Equal("firefox/profile 2", Log.ChoiceLabel("firefox/Jane", tiles));
        Assert.Equal("firefox/(profile gone)", Log.ChoiceLabel("firefox/Old", tiles));
        Assert.Equal(Tile.CopyId, Log.ChoiceLabel(Tile.CopyId, tiles));
    }

    /// <summary>Points the log at a temporary folder, with logging on or off, for the length of <paramref name="test"/>.</summary>
    static void WithLog(bool enabled, Action test)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"browserfork-log-{Guid.NewGuid():N}");
        var (savedPath, savedEnabled) = (Log.FilePath, Log.IsEnabled);
        try
        {
            Log.FilePath = Path.Combine(dir, "log.txt");
            Log.IsEnabled = () => enabled;
            test();
        }
        finally
        {
            (Log.FilePath, Log.IsEnabled) = (savedPath, savedEnabled);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Nothing_is_written_until_logging_is_turned_on()
    {
        WithLog(enabled: false, () =>
        {
            Log.Write("Link from outlook.exe");
            Assert.False(File.Exists(Log.FilePath));
        });
    }

    [Fact]
    public void Clear_deletes_the_log()
    {
        WithLog(enabled: true, () =>
        {
            Log.Write("Link from outlook.exe");
            Assert.True(File.Exists(Log.FilePath));

            Log.Clear();

            Assert.False(File.Exists(Log.FilePath));
        });
    }

    [Fact]
    public void Report_says_when_logging_is_off()
    {
        WithLog(enabled: false, () => Assert.Contains("(logging is off)", Log.Report([], new Settings(), autostart: false)));
    }

    [Fact]
    public void Report_lists_the_setup_and_the_log_without_personal_details()
    {
        WithLog(enabled: true, () =>
        {
            Log.Write("Link https://github.com/secret from outlook.exe");
            var settings = new Settings { Logging = true };
            settings.Rules["outlook.exe"] = new Rule { Choice = "edge", Private = true, App = "Outlook", ChoiceName = "Edge (Jane)" };
            var edge = new BrowserInfo("edge", "Edge", @"C:\Program Files\Edge\msedge.exe", "", "--inprivate", null);

            var report = Log.Report([edge], settings, autostart: true);

            Assert.Contains("edge: msedge.exe, 0 profile(s), private window --inprivate", report);
            Assert.Contains("outlook.exe → edge (private)", report);
            Assert.Contains("Link https://<host>/<path> from outlook.exe", report);
            Assert.DoesNotContain("logging is off", report);
            Assert.DoesNotContain("secret", report);
            Assert.DoesNotContain("Jane", report);
            Assert.DoesNotContain(Environment.UserName, report, StringComparison.OrdinalIgnoreCase);
        });
    }
}
