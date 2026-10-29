using System.IO;
using System.Text.Json;

namespace BrowserFork;

/// <summary>"Always open links from this app with …": skips the picker for that app.</summary>
sealed class Rule
{
    public string Choice { get; set; } = "";
    public bool Private { get; set; }
    /// <summary>The calling app's name, for showing the rule in Settings.</summary>
    public string App { get; set; } = "";
    /// <summary>The choice's name (browser and profile), for showing the rule in Settings.</summary>
    public string ChoiceName { get; set; } = "";
}

/// <summary>User preferences (%APPDATA%\BrowserFork\settings.json).</summary>
sealed class Settings
{
    /// <summary>Keyed by <see cref="CallerInfo.Key"/>.</summary>
    public Dictionary<string, Rule> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool AutoUpdate { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; }

    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrowserFork", "settings.json");

    public static Settings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) is { } s)
            {
                s.Rules = new(s.Rules, StringComparer.OrdinalIgnoreCase);
                return s;
            }
        }
        catch { }
        return new Settings();
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

    /// <summary>The tile a rule for <paramref name="caller"/> points at, if there is one and it still exists.</summary>
    public Tile? RuleTarget(CallerInfo? caller, IEnumerable<Tile> tiles) =>
        caller != null && Rules.TryGetValue(caller.Key, out var rule)
            ? tiles.FirstOrDefault(t => t.Id == rule.Choice && t.Browser != null)
            : null;
}
