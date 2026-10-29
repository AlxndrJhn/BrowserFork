using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BrowserFork;

/// <summary>
/// Floats next to Settings › Default apps and shows what HTTP and HTTPS should look like, ticking each one off
/// once Windows reports BrowserFork as its default. Windows doesn't let apps set this themselves.
/// </summary>
public partial class DefaultAppsGuide : Window
{
    sealed class ProtocolRow(string protocol, string label, string description) : INotifyPropertyChanged
    {
        public string Protocol { get; } = protocol;
        public string Label { get; } = label;
        public string Description { get; } = description;

        bool _isDone;
        public bool IsDone
        {
            get => _isDone;
            set { if (_isDone != value) { _isDone = value; PropertyChanged?.Invoke(this, new(nameof(IsDone))); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    static DefaultAppsGuide? _open;

    readonly ProtocolRow[] _rows =
    [
        new("http", "HTTP", "URL:HyperText Transfer Protocol"),
        new("https", "HTTPS", "URL:HyperText Transfer Protocol with Privacy"),
    ];
    readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1) };
    bool _closing;
    bool? _doneAtStart;

    /// <summary>Opens Settings › Default apps with the guide beside it. Needs a running WPF <see cref="Application"/>.</summary>
    public static void Open(bool openSettings = true)
    {
        if (openSettings) Registration.OpenDefaultAppsSettings();
        if (_open != null) { _open.Refresh(); return; }
        _open = new DefaultAppsGuide();
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    /// <summary>For <c>--set-default</c>: runs until the guide is closed.</summary>
    public static int Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
        app.Startup += (_, _) => Open();
        return app.Run();
    }

    DefaultAppsGuide()
    {
        InitializeComponent();
        Rows.ItemsSource = _rows;
        Refresh();
        _poll.Tick += (_, _) => Refresh();
        _poll.Start();
        Closed += (_, _) => _poll.Stop();

        // On the right edge of the screen, where the Settings window usually leaves room.
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 8;
            Top = area.Top + Math.Max(0, (area.Height - ActualHeight) / 2);
        };
    }

    void Refresh()
    {
        foreach (var row in _rows)
            row.IsDone = string.Equals(Native.DefaultProgId(row.Protocol), Registration.UrlProgId, StringComparison.OrdinalIgnoreCase);

        bool allDone = _rows.All(r => r.IsDone);
        _doneAtStart ??= allDone;
        if (allDone)
        {
            Footer.Foreground = (System.Windows.Media.Brush)FindResource("Done");
            Footer.Text = _doneAtStart == true ? "BrowserFork is already your default browser." : "All set. Links now open BrowserFork.";
            // Close on its own only once the user has just finished, not when it was opened to look.
            if (_doneAtStart == false && !_closing)
            {
                _closing = true;
                var close = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                close.Tick += (_, _) => { close.Stop(); Close(); };
                close.Start();
            }
        }
        else
        {
            int left = _rows.Count(r => !r.IsDone);
            Footer.Text = left == _rows.Length ? "This updates as you go." : "One more to go.";
        }
    }

    void OnOpenSettings(object sender, RoutedEventArgs e) => Registration.OpenDefaultAppsSettings();

    void OnClose(object sender, RoutedEventArgs e) => Close();

    void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
