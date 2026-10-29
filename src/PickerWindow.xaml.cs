using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace BrowserFork;

public partial class PickerWindow : Window
{
    readonly LaunchRequest _request;
    readonly CallerInfo? _caller;
    readonly HistoryStore _history;
    readonly List<Button> _buttons = [];

    internal PickerWindow(LaunchRequest request, CallerInfo? caller, List<BrowserInfo> browsers, HistoryStore history)
    {
        InitializeComponent();
        _request = request;
        _caller = caller;
        _history = history;

        var tiles = browsers
            .Select(b => new Tile { Id = b.Id, Name = b.Name, Icon = b.Icon, Browser = b })
            .Append(new Tile { Id = Tile.CopyId, Name = "Copy link", Glyph = "" })
            .ToList();
        for (int i = 0; i < tiles.Count; i++) tiles[i].Hotkey = i < 9 ? (i + 1).ToString() : "";

        int preselect = history.Suggest(tiles, caller, request.Host);

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
        ShowUrl();
        UrlText.MaxWidth = tiles.Count * 138 - 10;
        Hints.Text = $"Enter / Space open  ·  Shift+Enter private  ·  1–{Math.Min(tiles.Count, 9)}  ·  Ctrl+C copy  ·  Esc cancel";

        Loaded += (_, _) =>
        {
            Native.ForceForeground(new WindowInteropHelper(this).Handle);
            Activate();
            _buttons[preselect].Focus();
        };
    }

    string? TooltipFor(Tile t) => t.Browser == null
        ? "Copy the link to the clipboard instead of opening it"
        : t.Browser.PrivateFlag != null ? $"{t.Browser.ExePath}\nShift+click for a private window" : t.Browser.ExePath;

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
        try
        {
            if (tile.Browser == null)
                CopyLink();
            else
                Launch(tile.Browser, privateWindow);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not start {tile.Name}:\n{ex.Message}", "BrowserFork", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _history.Record(tile.Id, _caller?.Key, _request.Host);
        _history.Save();
        Close();
    }

    void Launch(BrowserInfo browser, bool privateWindow)
    {
        var args = string.Join(" ", new[] { browser.BaseArgs, privateWindow ? browser.PrivateFlag : null, _request.RawArgs }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        Process.Start(new ProcessStartInfo(browser.ExePath, args)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(browser.ExePath),
        });
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
