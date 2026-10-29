using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace BrowserFork;

/// <summary>The notification-area icon: settings, update checks and exit. Runs as <c>BrowserFork.exe --tray</c>.</summary>
sealed class TrayApp
{
    static readonly TimeSpan CheckEvery = TimeSpan.FromHours(24);

    readonly Application _app;
    readonly Forms.NotifyIcon _icon;
    SettingsWindow? _settingsWindow;
    ReleaseInfo? _available;
    bool _busy;

    public static int Run(bool showSettings = false)
    {
        using var mutex = new Mutex(true, @"Local\BrowserFork.Tray", out bool first);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (!first)
        {
            // The tray is already running; settings live in a file, so a standalone window is fine.
            if (!showSettings) return 0;
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            return app.Run(new SettingsWindow(checkForUpdates: null));
        }

        var tray = new TrayApp(app);
        if (showSettings) app.Startup += (_, _) => tray.ShowSettings();
        return app.Run();
    }

    TrayApp(Application app)
    {
        _app = app;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("Check for updates", null, async (_, _) => await CheckAsync(interactive: true));
        menu.Items.Add("Make default browser…", null, (_, _) => DefaultAppsGuide.Open());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(appIcon, Forms.SystemInformation.SmallIconSize),
            Text = $"BrowserFork {Updater.Current.ToString(3)}",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowSettings(); };
        _icon.BalloonTipClicked += async (_, _) => { if (_available != null) await InstallOrOpenAsync(_available); };

        // First check shortly after sign-in, then whenever a day has passed.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += async (_, _) =>
        {
            timer.Interval = TimeSpan.FromHours(1);
            if (DateTime.Now - Settings.Load().LastUpdateCheck >= CheckEvery) await CheckAsync(interactive: false);
        };
        timer.Start();
    }

    public void ShowSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(() => CheckAsync(interactive: true));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    async Task CheckAsync(bool interactive)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            ReleaseInfo? latest;
            try { latest = await Updater.LatestAsync(); }
            catch (Exception ex)
            {
                if (interactive) Balloon("Couldn't check for updates", ex.Message, Forms.ToolTipIcon.Warning);
                return;
            }

            var settings = Settings.Load();
            settings.LastUpdateCheck = DateTime.Now;
            settings.Save();

            if (latest == null || !Updater.IsNewer(latest))
            {
                if (interactive) Balloon("BrowserFork is up to date", $"Version {Updater.Current.ToString(3)}");
                return;
            }

            _available = latest;
            if (settings.AutoUpdate && Updater.CanSelfUpdate && latest.SetupUrl != null)
                await InstallOrOpenAsync(latest);
            else
                Balloon($"BrowserFork {latest.Tag} is available",
                    Updater.CanSelfUpdate && latest.SetupUrl != null ? "Click to install it." : "Click to open the release page.");
        }
        finally { _busy = false; }
    }

    async Task InstallOrOpenAsync(ReleaseInfo release)
    {
        if (!Updater.CanSelfUpdate || release.SetupUrl == null)
        {
            Updater.OpenPage(release.PageUrl);
            return;
        }
        try
        {
            Balloon($"Updating to BrowserFork {release.Tag}…", "Windows will ask for permission to install it.");
            await Updater.InstallAsync(release);
            Exit(); // the setup replaces our exe and starts the tray again
        }
        catch (Exception ex)
        {
            // Includes the user declining the admin prompt.
            Balloon("Update not installed", ex.Message, Forms.ToolTipIcon.Warning);
        }
    }

    void Balloon(string title, string text, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info) =>
        _icon.ShowBalloonTip(5000, title, text, icon);

    void Exit()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _app.Shutdown();
    }
}
