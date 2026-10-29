using System.Diagnostics;
using System.IO;

namespace BrowserFork;

static class Launcher
{
    public static void Open(BrowserInfo browser, BrowserProfile? profile, bool privateWindow, string rawArgs)
    {
        var args = BrowserCatalog.LaunchArgs(browser, profile, privateWindow, rawArgs);
        Process.Start(new ProcessStartInfo(browser.ExePath, args)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(browser.ExePath),
        });
    }

    /// <summary>All picker choices: browsers (one per profile when there are several), then "copy link".</summary>
    public static List<Tile> Tiles(IEnumerable<BrowserInfo> browsers)
    {
        var tiles = browsers
            .SelectMany(Tile.ForBrowser)
            .Append(new Tile { Id = Tile.CopyId, Name = "Copy link", Glyph = "" })
            .ToList();
        for (int i = 0; i < tiles.Count; i++) tiles[i].Hotkey = i < 9 ? (i + 1).ToString() : "";
        return tiles;
    }
}
