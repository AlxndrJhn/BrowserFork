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
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Crashed", (Exception)e.ExceptionObject, stack: true);

        Log.Write($"BrowserFork {Updater.Current.ToString(3)} {string.Join(' ', args)}".TrimEnd());

        switch (args.FirstOrDefault()?.ToLowerInvariant())
        {
            case "--set-default":
                return DefaultAppsGuide.Run();
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
                return answer == MessageBoxResult.Yes ? DefaultAppsGuide.Run() : 0;
        }

        var request = LaunchRequest.FromCommandLine(Environment.CommandLine, args);
        var caller = CallerDetector.Detect(foreground);
        var tiles = Launcher.Tiles(BrowserCatalog.Discover());
        var history = HistoryStore.Load();
        var settings = Settings.Load();
        Log.Write($"Link from {caller?.Key ?? "an unknown app"}; choices: {string.Join(", ", tiles.Select(t => t.LogId))}");

        // "Always open links from this app with …", unless Shift is held to get the picker back.
        if (!Native.IsShiftDown() && settings.RuleTarget(caller, tiles) is { Browser: { } browser } target)
        {
            try
            {
                bool privateWindow = settings.Rules[caller!.Key].Private;
                Log.Write($"Rule for {caller.Key}: {target.LogId}{(privateWindow ? " (private)" : "")}");
                Launcher.Open(browser, target.Profile, privateWindow, request.RawArgs);
                history.Record(target.Id, caller.Key, request.Host);
                history.Save();
                return 0;
            }
            catch (Exception ex) { Log.Error("Couldn't follow the rule, asking instead", ex); }
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
