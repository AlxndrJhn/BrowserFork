using System.IO;
using System.Text.Json;

namespace BrowserFork;

sealed class HistoryEntry
{
    public string Choice { get; set; } = "";
    public DateTime When { get; set; }
}

/// <summary>Remembers the last choice per calling app, per host and overall (%APPDATA%\BrowserFork\history.json).</summary>
sealed class HistoryStore
{
    public Dictionary<string, HistoryEntry> Callers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HistoryEntry> Hosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HistoryEntry? Last { get; set; }

    static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrowserFork", "history.json");

    public static HistoryStore Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var h = JsonSerializer.Deserialize<HistoryStore>(File.ReadAllText(path));
                if (h != null)
                {
                    h.Callers = new(h.Callers, StringComparer.OrdinalIgnoreCase);
                    h.Hosts = new(h.Hosts, StringComparer.OrdinalIgnoreCase);
                    return h;
                }
            }
        }
        catch { }
        return new HistoryStore();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public void Record(string choice, string? callerKey, string? host)
    {
        var entry = () => new HistoryEntry { Choice = choice, When = DateTime.Now };
        if (callerKey != null) Callers[callerKey] = entry();
        if (host != null) Hosts[host] = entry();
        Last = entry();
    }

    /// <summary>
    /// Annotates tiles with "last used …" notes and returns the index to preselect.
    /// Priority: same calling app &gt; same host &gt; most recent choice overall.
    /// </summary>
    public int Suggest(IList<Tile> tiles, CallerInfo? caller, string? host)
    {
        int best = -1, bestScore = 0;
        DateTime bestWhen = default;

        void Consider(HistoryEntry? e, int score, string note)
        {
            if (e == null) return;
            int i = tiles.ToList().FindIndex(t => t.Id == e.Choice);
            if (i < 0) return;
            if (score > 1 || tiles[i].Note == null)
                tiles[i].Note = tiles[i].Note == null ? note : tiles[i].Note + "\n" + note;
            if (score > bestScore || (score == bestScore && e.When > bestWhen))
                (best, bestScore, bestWhen) = (i, score, e.When);
        }

        if (caller != null) Consider(Callers.GetValueOrDefault(caller.Key), 3, $"Last used from {caller.DisplayName}");
        if (host != null) Consider(Hosts.GetValueOrDefault(host), 2, $"Last used for {host}");
        Consider(Last, 1, "Used last time");

        return best < 0 ? 0 : best;
    }
}
