using System.IO;
using Xunit;

namespace BrowserFork.Tests;

public class SettingsTests
{
    static readonly CallerInfo Outlook = new("outlook.exe", "Outlook", @"C:\Office\outlook.exe", null);
    static readonly BrowserInfo Edge = new("edge", "Edge", "msedge.exe", "", "--inprivate", null);

    static List<Tile> Tiles() => Launcher.Tiles([Edge]);

    [Fact]
    public void A_rule_for_the_caller_picks_its_tile()
    {
        var s = new Settings();
        s.Rules["Outlook.exe"] = new Rule { Choice = "edge" };

        Assert.Equal("edge", s.RuleTarget(Outlook, Tiles())?.Id);
    }

    [Fact]
    public void No_rule_without_a_caller_or_for_a_browser_that_is_gone()
    {
        var s = new Settings();
        s.Rules["outlook.exe"] = new Rule { Choice = "chrome" };

        Assert.Null(s.RuleTarget(null, Tiles()));
        Assert.Null(s.RuleTarget(Outlook, Tiles()));
    }

    [Fact]
    public void Copy_link_never_acts_as_a_rule()
    {
        var s = new Settings();
        s.Rules["outlook.exe"] = new Rule { Choice = Tile.CopyId };

        Assert.Null(s.RuleTarget(Outlook, Tiles()));
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"browserfork-test-{Guid.NewGuid():N}.json");
        try
        {
            var s = new Settings { AutoUpdate = false };
            s.Rules["Teams.exe"] = new Rule { Choice = "edge/Profile 1", Private = true, App = "Teams", ChoiceName = "Edge (Work)" };
            s.Save(path);

            var loaded = Settings.Load(path);

            Assert.False(loaded.AutoUpdate);
            var rule = loaded.Rules["teams.exe"];
            Assert.Equal("edge/Profile 1", rule.Choice);
            Assert.True(rule.Private);
            Assert.Equal("Edge (Work)", rule.ChoiceName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Defaults_to_auto_update_when_there_is_no_file()
    {
        Assert.True(Settings.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json")).AutoUpdate);
    }
}
