using System.Windows;

namespace BrowserFork;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Grab this before anything else: it's the window the link was clicked in.
        var foreground = Native.GetForegroundWindow();

        switch (args.FirstOrDefault()?.ToLowerInvariant())
        {
            case "--unregister":
                Registration.Unregister();
                MessageBox.Show("BrowserFork was removed from the list of browsers.", "BrowserFork");
                return 0;
            case "--register-machine":
                // Needs an elevated process; writes the same keys under HKLM, like machine-wide browser installs.
                try { Registration.Register(machine: true); }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
                {
                    MessageBox.Show("Run this as administrator to register BrowserFork for all users.", "BrowserFork");
                    return 1;
                }
                return 0;
            case null or "--register":
                Registration.Register();
                var answer = MessageBox.Show(
                    "BrowserFork is registered as a browser.\n\n" +
                    "To use it, choose BrowserFork as your default for HTTP and HTTPS in\n" +
                    "Settings › Apps › Default apps.\n\nOpen that page now?",
                    "BrowserFork", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes) Registration.OpenDefaultAppsSettings();
                return 0;
        }

        var request = LaunchRequest.FromCommandLine(Environment.CommandLine, args);
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var window = new PickerWindow(request, CallerDetector.Detect(foreground), BrowserCatalog.Discover(), HistoryStore.Load());
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
