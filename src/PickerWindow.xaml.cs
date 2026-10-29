using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace BrowserFork;

public partial class PickerWindow : Window
{
    const int TilesPerRow = 6;

    readonly LaunchRequest _request;
    readonly CallerInfo? _caller;
    readonly HistoryStore _history;
    readonly Settings _settings;
    readonly List<Button> _buttons = [];
    bool _chosen;

    internal PickerWindow(LaunchRequest request, CallerInfo? caller, List<Tile> tiles, HistoryStore history, Settings settings)
    {
        InitializeComponent();
        _request = request;
        _caller = caller;
        _history = history;
        _settings = settings;

        int preselect = history.Suggest(tiles, caller, request.Host);
        Log.Write($"Picker, preselected {tiles[preselect].LogId}");

        foreach (var tile in tiles)
        {
            var button = new Button { Style = (Style)FindResource("TileButton"), DataContext = tile, ToolTip = TooltipFor(tile) };
            button.Click += (_, _) => Choose(tile, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            _buttons.Add(button);
            TilePanel.Children.Add(button);
        }

        CallerIcon.Source = caller?.Icon;
        CallerIcon.Visibility = caller?.Icon == null ? Visibility.Collapsed : Visibility.Visible;
        CallerText.Text = caller == null ? "Open link" : $"Open link from {caller.DisplayName}";
        AlwaysBox.Visibility = caller == null ? Visibility.Collapsed : Visibility.Visible;
        AlwaysBox.Content = $"_Always open links from {caller?.DisplayName} this way";
        ShowUrl();
        TilePanel.MaxWidth = TilesPerRow * 138;
        UrlText.MaxWidth = Math.Min(tiles.Count, TilesPerRow) * 138 - 10;
        Hints.Text = $"Enter / Space open  ·  Shift+Enter private  ·  1–{Math.Min(tiles.Count, 9)}  ·  Ctrl+C copy{(caller == null ? "" : "  ·  Alt+A always")}  ·  Esc cancel";

        Closed += (_, _) => { if (!_chosen) Log.Write("Picker closed without a choice"); };
        Loaded += (_, _) =>
        {
            Native.ForceForeground(new WindowInteropHelper(this).Handle);
            Activate();
            _buttons[preselect].Focus();
        };
    }

    static string? TooltipFor(Tile t)
    {
        if (t.Browser == null) return "Copy the link to the clipboard instead of opening it";
        var lines = new List<string> { t.Browser.ExePath };
        if (t.Profile != null) lines.Add($"Profile: {t.Profile.Name}");
        if (t.Browser.PrivateFlag != null) lines.Add("Shift+click for a private window");
        return string.Join("\n", lines);
    }

    void ShowUrl()
    {
        var text = _request.Url ?? _request.RawArgs;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            UrlHost.Text = uri.Host;
            UrlRest.Text = uri.PathAndQuery == "/" && string.IsNullOrEmpty(uri.Fragment) ? "" : uri.PathAndQuery + uri.Fragment;
        }
        else
        {
            UrlHost.Text = text.Length == 0 ? "(no link)" : text;
            UrlRest.Text = "";
        }
        UrlText.ToolTip = text;
    }

    void Choose(Tile tile, bool privateWindow)
    {
        Log.Write($"Chose {tile.LogId}{(privateWindow ? " (private)" : "")}{(AlwaysBox.IsChecked == true ? ", always" : "")}");
        try
        {
            if (tile.Browser == null)
                CopyLink();
            else
                Launcher.Open(tile.Browser, tile.Profile, privateWindow, _request.RawArgs);
        }
        catch (Exception ex)
        {
            Log.Error($"Couldn't start {tile.LogId}", ex);
            MessageBox.Show(this, $"Could not start {tile.Name}:\n{ex.Message}", "BrowserFork", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _history.Record(tile.Id, _caller?.Key, _request.Host);
        _history.Save();
        // "Copy link" can't be a rule: the link would silently vanish into the clipboard every time.
        if (AlwaysBox.IsChecked == true && _caller != null && tile.Browser != null)
        {
            _settings.Rules[_caller.Key] = new Rule
            {
                Choice = tile.Id,
                Private = privateWindow,
                App = _caller.DisplayName,
                ChoiceName = tile.Profile == null ? tile.Name : $"{tile.Name} ({tile.Profile.Name})",
            };
            _settings.Save();
        }
        _chosen = true;
        Close();
    }

    void CopyLink()
    {
        var text = _request.Url ?? _request.RawArgs;
        // The clipboard is sometimes briefly locked by another process.
        for (int attempt = 0; ; attempt++)
        {
            try { Clipboard.SetDataObject(text, copy: true); return; }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 5) { Thread.Sleep(30); }
        }
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Enter or Key.Space:
                if (Keyboard.FocusedElement is Button { DataContext: Tile focused }) Choose(focused, shift);
                break;
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                Choose((Tile)_buttons[^1].DataContext, false);
                break;
            case >= Key.D1 and <= Key.D9 or >= Key.NumPad1 and <= Key.NumPad9:
                int index = key >= Key.NumPad1 ? key - Key.NumPad1 : key - Key.D1;
                if (index < _buttons.Count) Choose((Tile)_buttons[index].DataContext, shift);
                break;
            case Key.Right or Key.Down:
                MoveFocus(+1);
                break;
            case Key.Left or Key.Up:
                MoveFocus(-1);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    void MoveFocus(int delta)
    {
        int current = _buttons.FindIndex(b => b.IsKeyboardFocused);
        int next = ((current < 0 ? 0 : current + delta) + _buttons.Count) % _buttons.Count;
        _buttons[next].Focus();
    }

    void OnCancel(object sender, RoutedEventArgs e) => Close();

    void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
