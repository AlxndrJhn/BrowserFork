using System.Windows;

namespace BrowserFork;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Grab this before anything else: it's the window the link was clicked in.
        var foreground = Native.GetForegroundWindow();
        // For the installer and updater, which run these without anyone watching.
        bool quiet = args.Skip(1).Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase));

        switch (args.FirstOrDefault()?.ToLowerInvariant())
        {
            case "--tray":
                return TrayApp.Run();
            case "--settings":
                return TrayApp.Run(showSettings: true);
            case "--unregister":
                Registration.Unregister();
                if (!quiet) MessageBox.Show("BrowserFork was removed from the list of browsers.", "BrowserFork");
                return 0;
            case "--register-machine":
                // Needs an elevated process; writes the same keys under HKLM, like machine-wide browser installs.
                try { Registration.Register(machine: true); }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
                {
                    if (!quiet) MessageBox.Show("Run this as administrator to register BrowserFork for all users.", "BrowserFork");
                    return 1;
                }
                return 0;
            case null or "--register":
                Registration.Register();
                if (quiet) return 0;
                var answer = MessageBox.Show(
                    "BrowserFork is registered as a browser.\n\n" +
                    "To use it, choose BrowserFork as your default for HTTP and HTTPS in\n" +
                    "Settings › Apps › Default apps.\n\nOpen that page now?",
                    "BrowserFork", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes) Registration.OpenDefaultAppsSettings();
                return 0;
        }

        var request = LaunchRequest.FromCommandLine(Environment.CommandLine, args);
        var caller = CallerDetector.Detect(foreground);
        var tiles = Launcher.Tiles(BrowserCatalog.Discover());
        var history = HistoryStore.Load();
        var settings = Settings.Load();

        // "Always open links from this app with …", unless Shift is held to get the picker back.
        if (!Native.IsShiftDown() && settings.RuleTarget(caller, tiles) is { Browser: { } browser } target)
        {
            try
            {
                Launcher.Open(browser, target.Profile, settings.Rules[caller!.Key].Private, request.RawArgs);
                history.Record(target.Id, caller.Key, request.Host);
                history.Save();
                return 0;
            }
            catch { } // fall back to asking
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var window = new PickerWindow(request, caller, tiles, history, settings);
        return app.Run(window);
    }
}

/// <summary>What we were asked to open, kept verbatim so it can be forwarded unchanged.</summary>
sealed record LaunchRequest(string RawArgs, string? Url, string? Host)
{
    public static LaunchRequest FromCommandLine(string commandLine, string[] args)
    {
        // Everything after our own exe path, byte-for-byte.
        string cl = commandLine.TrimStart();
        int i;
        if (cl.StartsWith('"')) { i = cl.IndexOf('"', 1); i = i < 0 ? cl.Length : i + 1; }
        else { i = 0; while (i < cl.Length && !char.IsWhiteSpace(cl[i])) i++; }
        string raw = cl[i..].Trim();

        string? url = args.FirstOrDefault(a => !a.StartsWith('-')) ?? args.FirstOrDefault();
        string? host = null;
        if (url != null && Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;

        return new LaunchRequest(raw, url, host);
    }
}
