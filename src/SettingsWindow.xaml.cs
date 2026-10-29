using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BrowserFork;

public partial class SettingsWindow : Window
{
    sealed record RuleRow(string Key, string App, string Target);

    readonly Func<Task>? _checkForUpdates;

    /// <param name="checkForUpdates">Runs a check through the tray; null when no tray is attached to this window.</param>
    internal SettingsWindow(Func<Task>? checkForUpdates)
    {
        InitializeComponent();
        _checkForUpdates = checkForUpdates;

        VersionText.Text = $"Version {Updater.Current.ToString(3)}" +
                           (Updater.CanSelfUpdate ? "" : "\nNot installed by the setup, so new versions are announced but not installed.");
        var settings = Settings.Load();
        AutoUpdateBox.IsChecked = settings.AutoUpdate;
        AutoUpdateBox.IsEnabled = Updater.CanSelfUpdate;
        AutostartBox.IsChecked = Registration.Autostart;
        LoggingBox.IsChecked = settings.Logging;
        CheckButton.Visibility = checkForUpdates == null ? Visibility.Collapsed : Visibility.Visible;
        ShowRules(settings);

        AutoUpdateBox.Click += (_, _) => Change(s => s.AutoUpdate = AutoUpdateBox.IsChecked == true);
        AutostartBox.Click += (_, _) => Registration.Autostart = AutostartBox.IsChecked == true;
        LoggingBox.Click += (_, _) => OnLogging(LoggingBox.IsChecked == true);
        Activated += (_, _) => ShowRules(Settings.Load());
    }

    void ShowRules(Settings settings)
    {
        RuleList.ItemsSource = settings.Rules
            .OrderBy(r => r.Value.App, StringComparer.OrdinalIgnoreCase)
            .Select(r => new RuleRow(r.Key, r.Value.App, r.Value.ChoiceName + (r.Value.Private ? " (private)" : "")))
            .ToList();
        NoRulesText.Visibility = settings.Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Reload before every change: the picker may have added a rule while this window was open.
    void Change(Action<Settings> change)
    {
        var settings = Settings.Load();
        change(settings);
        settings.Save();
        ShowRules(settings);
    }

    void OnRemoveRule(object sender, RoutedEventArgs e) =>
        Change(s => s.Rules.Remove((string)((Button)sender).Tag));

    async void OnCheck(object sender, RoutedEventArgs e)
    {
        if (_checkForUpdates == null) return;
        CheckButton.IsEnabled = false;
        try { await _checkForUpdates(); }
        finally { CheckButton.IsEnabled = true; }
    }

    void OnDefaultApps(object sender, RoutedEventArgs e) => DefaultAppsGuide.Open();

    void OnLogging(bool on)
    {
        Change(s => s.Logging = on);
        if (on) Log.Write($"Logging turned on, BrowserFork {Updater.Current.ToString(3)}");
        else Log.Clear();
    }

    void OnSaveLog(object sender, RoutedEventArgs e)
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var dialog = new SaveFileDialog
        {
            Title = "Save anonymized log",
            FileName = $"BrowserFork-log-{DateTime.Now:yyyy-MM-dd}.txt",
            Filter = "Text files|*.txt",
            InitialDirectory = Directory.Exists(downloads) ? downloads : null,
        };
        if (dialog.ShowDialog(this) != true) return;

        try { File.WriteAllText(dialog.FileName, Log.Report(BrowserCatalog.Discover(), Settings.Load(), Registration.Autostart)); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save the log:\n{ex.Message}", "BrowserFork", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var answer = MessageBox.Show(this,
            $"Saved {Path.GetFileName(dialog.FileName)}. Have a look before you share it.\n\nOpen GitHub to report the problem?",
            "BrowserFork", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer != MessageBoxResult.Yes) return;
        Updater.OpenPage($"https://github.com/{Updater.Repo}/issues/new");
        // So the file is at hand to drag onto the issue.
        Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
    }

    // Someone may have reported it already.
    void OnOpenIssues(object sender, RoutedEventArgs e) => Updater.OpenPage($"https://github.com/{Updater.Repo}/issues");

    void OnClose(object sender, RoutedEventArgs e) => Close();
}
