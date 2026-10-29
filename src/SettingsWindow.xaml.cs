using System.Windows;
using System.Windows.Controls;

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
        CheckButton.Visibility = checkForUpdates == null ? Visibility.Collapsed : Visibility.Visible;
        ShowRules(settings);

        AutoUpdateBox.Click += (_, _) => Change(s => s.AutoUpdate = AutoUpdateBox.IsChecked == true);
        AutostartBox.Click += (_, _) => Registration.Autostart = AutostartBox.IsChecked == true;
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

    void OnDefaultApps(object sender, RoutedEventArgs e) => Registration.OpenDefaultAppsSettings();

    void OnClose(object sender, RoutedEventArgs e) => Close();
}
