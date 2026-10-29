using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace BrowserFork;

/// <summary>
/// A small diagnostic log (%APPDATA%\BrowserFork\log.txt) for issue reports, off until the user turns it on in
/// Settings. Every line goes through <see cref="Anonymizer"/> first, so the file never holds links, user names or
/// personal paths.
/// </summary>
static class Log
{
    const long MaxBytes = 512 * 1024;

    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrowserFork", "log.txt");

    /// <summary>Overridden by tests.</summary>
    public static string FilePath { get; set; } = DefaultPath;

    /// <summary>The previous log, kept once <see cref="FilePath"/> outgrows <see cref="MaxBytes"/>.</summary>
    static string OldPath => Path.ChangeExtension(FilePath, ".old.txt");

    /// <summary>Read on every write, so turning logging on or off reaches the tray at once. Overridden by tests.</summary>
    public static Func<bool> IsEnabled { get; set; } = () => Settings.Load().Logging;

    // Settings.Load logs its own errors, which would ask IsEnabled again.
    [ThreadStatic] static bool _writing;

    public static void Write(string message)
    {
        if (_writing) return;
        _writing = true;
        try
        {
            if (!IsEnabled()) return;
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {Anonymizer.Scrub(message)}{Environment.NewLine}";
            // The picker and the tray can write at the same time.
            using var mutex = new Mutex(false, @"Local\BrowserFork.Log");
            bool owned;
            try { owned = mutex.WaitOne(1000); }
            catch (AbandonedMutexException) { owned = true; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes) File.Move(FilePath, OldPath, overwrite: true);
                File.AppendAllText(FilePath, line);
            }
            finally { if (owned) mutex.ReleaseMutex(); }
        }
        catch { }
        finally { _writing = false; }
    }

    /// <summary>Deletes the log, for when the user turns logging off.</summary>
    public static void Clear()
    {
        foreach (var file in new[] { OldPath, FilePath })
            try { File.Delete(file); } catch { }
    }

    public static void Error(string what, Exception ex, bool stack = false) =>
        Write($"{what}: {(stack ? ex.ToString() : $"{ex.GetType().Name}: {ex.Message}")}");

    /// <summary>The setup and the log, anonymized, for attaching to an issue.</summary>
    public static string Report(IReadOnlyList<BrowserInfo> browsers, Settings settings, bool autostart)
    {
        var tiles = Launcher.Tiles(browsers);
        var sb = new StringBuilder();
        sb.AppendLine($"BrowserFork {Updater.Current.ToString(3)} ({(Updater.CanSelfUpdate ? "setup install" : "not installed by the setup")})");
        sb.AppendLine($"Windows {Environment.OSVersion.Version} {RuntimeInformation.OSArchitecture}, .NET {Environment.Version}");
        sb.AppendLine($"Tray starts with Windows: {(autostart ? "yes" : "no")}, automatic updates: {(settings.AutoUpdate ? "on" : "off")}");

        sb.AppendLine().AppendLine("Browsers:");
        foreach (var b in browsers)
            sb.AppendLine($"  {b.Id}: {Path.GetFileName(b.ExePath)} {b.BaseArgs}".TrimEnd() +
                          $", {b.Profiles.Count} profile(s), private window {b.PrivateFlag ?? "not supported"}");
        if (browsers.Count == 0) sb.AppendLine("  none found");

        sb.AppendLine().AppendLine("Always open links from:");
        foreach (var (app, rule) in settings.Rules.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"  {app} → {ChoiceLabel(rule.Choice, tiles)}{(rule.Private ? " (private)" : "")}");
        if (settings.Rules.Count == 0) sb.AppendLine("  no apps");

        sb.AppendLine().AppendLine("Log:");
        if (!settings.Logging) sb.AppendLine("(logging is off)");
        foreach (var file in new[] { OldPath, FilePath })
            try { if (File.Exists(file)) sb.Append(File.ReadAllText(file)); }
            catch (IOException ex) { sb.AppendLine($"(couldn't read {Path.GetFileName(file)}: {ex.Message})"); }

        // The log was scrubbed when written; this also covers the summary and older entries.
        return Anonymizer.Scrub(sb.ToString());
    }

    /// <summary>A choice id with the profile's name, which the user picked, swapped for its position.</summary>
    public static string ChoiceLabel(string choice, IEnumerable<Tile> tiles) =>
        tiles.FirstOrDefault(t => t.Id == choice)?.LogId
        ?? (choice.IndexOf('/') is var slash && slash > 0 ? choice[..slash] + "/(profile gone)" : choice);
}

/// <summary>Strips links, e-mail addresses, user names, the machine name and personal paths from text.</summary>
static class Anonymizer
{
    static readonly Regex Url = new(@"\b([a-z][a-z0-9+.\-]+)://[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex Email = new(@"[a-z0-9._%+\-]+@[a-z0-9\-]+(\.[a-z0-9\-]+)+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex UsersFolder = new(@"(?<=[a-z]:[\\/]Users[\\/])[^\\/\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Scrub(string text) => Scrub(text,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        [Environment.UserName, Environment.MachineName, Environment.UserDomainName]);

    public static string Scrub(string text, string? userProfile, IEnumerable<string?> names)
    {
        text = Url.Replace(text, m => Shape(m.Groups[1].Value, m.Value));
        text = Email.Replace(text, "<email>");
        if (!string.IsNullOrEmpty(userProfile)) text = text.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        text = UsersFolder.Replace(text, "<user>");
        foreach (var name in names.Where(n => n is { Length: >= 2 }).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(n => n!.Length))
            text = Regex.Replace(text, $@"(?<![a-z0-9<]){Regex.Escape(name!)}(?![a-z0-9>])", "<name>", RegexOptions.IgnoreCase);
        return text;
    }

    /// <summary><c>https://github.com/a/b?q=1</c> → <c>https://&lt;host&gt;/&lt;path&gt;?&lt;query&gt;</c>: which parts there were, not what they said.</summary>
    static string Shape(string scheme, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return $"{scheme}://<link>";
        var sb = new StringBuilder($"{scheme}://<host>");
        if (!uri.IsDefaultPort) sb.Append(":<port>");
        if (uri.AbsolutePath.Length > 1) sb.Append("/<path>");
        if (uri.Query.Length > 0) sb.Append("?<query>");
        if (uri.Fragment.Length > 0) sb.Append("#<fragment>");
        return sb.ToString();
    }
}
