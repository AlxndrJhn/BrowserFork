using System.ComponentModel;
using System.Windows.Media;

namespace BrowserFork;

/// <summary>One choice in the picker: a browser, or "copy link".</summary>
sealed class Tile : INotifyPropertyChanged
{
    public const string CopyId = "::copy";

    public required string Id { get; init; }
    public required string Name { get; init; }
    public ImageSource? Icon { get; init; }
    /// <summary>Segoe Fluent glyph shown when there is no icon.</summary>
    public string? Glyph { get; init; }
    public BrowserInfo? Browser { get; init; }
    public string Hotkey { get; set; } = "";

    string? _note;
    public string? Note
    {
        get => _note;
        set { _note = value; PropertyChanged?.Invoke(this, new(nameof(Note))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
