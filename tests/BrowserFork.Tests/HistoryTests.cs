using System.IO;
using Xunit;

namespace BrowserFork.Tests;

public class HistoryTests
{
    static readonly CallerInfo Outlook = new("outlook.exe", "Outlook", @"C:\Office\outlook.exe", null);
    static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    static List<Tile> Tiles(params string[] ids) =>
        ids.Select(id => new Tile { Id = id, Name = id }).ToList();

    static HistoryEntry At(string choice, int minutes) => new() { Choice = choice, When = T0.AddMinutes(minutes) };

    [Fact]
    public void Without_history_the_first_tile_is_selected_and_nothing_is_annotated()
    {
        var tiles = Tiles("edge", "chrome");

        Assert.Equal(0, new HistoryStore().Suggest(tiles, Outlook, "github.com"));
        Assert.All(tiles, t => Assert.Null(t.Note));
    }

    [Fact]
    public void Same_caller_wins_over_same_host_and_last_overall()
    {
        var h = new HistoryStore { Last = At("firefox", 30) };
        h.Hosts["github.com"] = At("chrome", 20);
        h.Callers["outlook.exe"] = At("edge", 10);
        var tiles = Tiles("edge", "chrome", "firefox");

        Assert.Equal(0, h.Suggest(tiles, Outlook, "github.com"));
        Assert.Equal("Last used from Outlook", tiles[0].Note);
        Assert.Equal("Last used for github.com", tiles[1].Note);
    }

    [Fact]
    public void Same_host_is_used_when_the_caller_is_unknown()
    {
        var h = new HistoryStore { Last = At("firefox", 30) };
        h.Hosts["github.com"] = At("chrome", 20);
        var tiles = Tiles("edge", "chrome", "firefox");

        Assert.Equal(1, h.Suggest(tiles, caller: null, "github.com"));
    }

    [Fact]
    public void Falls_back_to_the_last_choice_overall()
    {
        var h = new HistoryStore { Last = At("chrome", 0) };
        var tiles = Tiles("edge", "chrome");

        Assert.Equal(1, h.Suggest(tiles, Outlook, "example.org"));
        Assert.Equal("Used last time", tiles[1].Note);
    }

    [Fact]
    public void Used_last_time_is_not_added_on_top_of_a_more_specific_note()
    {
        var h = new HistoryStore { Last = At("edge", 5) };
        h.Callers["outlook.exe"] = At("edge", 5);
        var tiles = Tiles("edge");

        h.Suggest(tiles, Outlook, null);

        Assert.Equal("Last used from Outlook", tiles[0].Note);
    }

    [Fact]
    public void Caller_and_host_notes_stack_on_the_same_tile()
    {
        var h = new HistoryStore();
        h.Callers["outlook.exe"] = At("edge", 1);
        h.Hosts["github.com"] = At("edge", 1);
        var tiles = Tiles("edge", "chrome");

        h.Suggest(tiles, Outlook, "github.com");

        Assert.Equal("Last used from Outlook\nLast used for github.com", tiles[0].Note);
    }

    [Fact]
    public void Choices_of_browsers_that_are_no_longer_installed_are_ignored()
    {
        var h = new HistoryStore { Last = At("chrome", 0) };
        h.Callers["outlook.exe"] = At("opera", 10);
        var tiles = Tiles("edge", "chrome");

        Assert.Equal(1, h.Suggest(tiles, Outlook, null));
        Assert.Null(tiles[0].Note);
    }

    [Fact]
    public void Copy_link_takes_part_like_a_browser()
    {
        var h = new HistoryStore();
        h.Record(Tile.CopyId, "outlook.exe", "github.com");
        var tiles = Tiles("edge", Tile.CopyId);

        Assert.Equal(1, h.Suggest(tiles, Outlook, "github.com"));
    }

    [Fact]
    public void Record_updates_caller_host_and_last()
    {
        var h = new HistoryStore();

        h.Record("chrome", "outlook.exe", "github.com");

        Assert.Equal("chrome", h.Callers["outlook.exe"].Choice);
        Assert.Equal("chrome", h.Hosts["github.com"].Choice);
        Assert.Equal("chrome", h.Last!.Choice);
    }

    [Fact]
    public void Round_trips_through_json_with_case_insensitive_keys()
    {
        var path = Path.Combine(Path.GetTempPath(), $"browserfork-test-{Guid.NewGuid():N}.json");
        try
        {
            var h = new HistoryStore();
            h.Record("firefox", "Teams.exe", "GitHub.com");
            h.Save(path);

            var loaded = HistoryStore.Load(path);

            Assert.Equal("firefox", loaded.Callers["teams.exe"].Choice);
            Assert.Equal("firefox", loaded.Hosts["github.com"].Choice);
            Assert.Equal("firefox", loaded.Last!.Choice);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_corrupt_history_file_loads_as_empty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"browserfork-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            var loaded = HistoryStore.Load(path);

            Assert.Empty(loaded.Callers);
            Assert.Null(loaded.Last);
        }
        finally { File.Delete(path); }
    }
}
