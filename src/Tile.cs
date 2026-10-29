using System.ComponentModel;
using System.Windows.Media;

namespace BrowserFork;

/// <summary>One choice in the picker: a browser (or one of its profiles), or "copy link".</summary>
sealed class Tile : INotifyPropertyChanged
{
    public const string CopyId = "::copy";

    public required string Id { get; init; }
    public required string Name { get; init; }
    public ImageSource? Icon { get; init; }
    /// <summary>Segoe Fluent glyph shown when there is no icon.</summary>
    public string? Glyph { get; init; }
    public BrowserInfo? Browser { get; init; }
    public BrowserProfile? Profile { get; init; }
    public string? ProfileName => Profile?.Name;
    public string Hotkey { get; set; } = "";

    /// <summary><see cref="Id"/> for the log: the profile by its position, as its name may be the user's own.</summary>
    public string LogId => Browser is { } b && Profile is { } p ? $"{b.Id}/profile {b.Profiles.ToList().IndexOf(p) + 1}" : Id;

    string? _note;
    public string? Note
    {
        get => _note;
        set { _note = value; PropertyChanged?.Invoke(this, new(nameof(Note))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>One tile per profile when the browser has several, otherwise a single tile that leaves the choice to the browser.</summary>
    public static IEnumerable<Tile> ForBrowser(BrowserInfo b) => b.Profiles.Count > 1
        ? b.Profiles.Select(p => new Tile { Id = $"{b.Id}/{p.Key}", Name = b.Name, Icon = b.Icon, Browser = b, Profile = p })
        : [new Tile { Id = b.Id, Name = b.Name, Icon = b.Icon, Browser = b }];
}
