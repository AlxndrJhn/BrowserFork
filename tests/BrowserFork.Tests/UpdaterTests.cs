using Xunit;

namespace BrowserFork.Tests;

public class UpdaterTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3.0")]
    [InlineData("1.2", "1.2.0.0")]
    [InlineData("v2.0.1-beta.1", "2.0.1.0")]
    public void Parses_release_tags(string tag, string version)
    {
        Assert.Equal(Version.Parse(version), Updater.ParseTag(tag));
    }

    [Fact]
    public void Tags_that_are_not_versions_are_ignored()
    {
        Assert.Null(Updater.ParseTag("nightly"));
    }

    [Fact]
    public void Reads_the_setup_asset_from_the_github_release()
    {
        var release = Updater.ParseRelease("""
            { "tag_name": "v9.1.0", "html_url": "https://github.com/AlxndrJhn/BrowserFork/releases/tag/v9.1.0",
              "assets": [
                { "name": "BrowserFork.exe", "browser_download_url": "https://example.test/BrowserFork.exe" },
                { "name": "BrowserFork-Setup.exe", "browser_download_url": "https://example.test/BrowserFork-Setup.exe" } ] }
            """)!;

        Assert.Equal(new Version(9, 1, 0, 0), release.Version);
        Assert.Equal("https://example.test/BrowserFork-Setup.exe", release.SetupUrl);
        Assert.True(Updater.IsNewer(release));
    }

    [Fact]
    public void The_running_version_is_not_newer_than_itself()
    {
        var current = Updater.Current;
        Assert.False(Updater.IsNewer(new ReleaseInfo(current, $"v{current}", "", null)));
    }
}
