using Xunit;

namespace BrowserFork.Tests;

public class LaunchRequestTests
{
    [Fact]
    public void Keeps_everything_after_a_quoted_exe_path_verbatim()
    {
        var r = LaunchRequest.FromCommandLine(
            "\"C:\\Program Files\\BrowserFork\\BrowserFork.exe\" \"https://example.com/a b?x=1\"",
            ["https://example.com/a b?x=1"]);

        Assert.Equal("\"https://example.com/a b?x=1\"", r.RawArgs);
        Assert.Equal("https://example.com/a b?x=1", r.Url);
    }

    [Fact]
    public void Keeps_everything_after_an_unquoted_exe_path()
    {
        var r = LaunchRequest.FromCommandLine("BrowserFork.exe   https://example.com  --flag", ["https://example.com", "--flag"]);

        Assert.Equal("https://example.com  --flag", r.RawArgs);
        Assert.Equal("https://example.com", r.Url);
    }

    [Fact]
    public void Url_is_the_first_argument_that_is_not_a_flag()
    {
        var r = LaunchRequest.FromCommandLine("x.exe --new-window https://github.com", ["--new-window", "https://github.com"]);

        Assert.Equal("https://github.com", r.Url);
        Assert.Equal("--new-window https://github.com", r.RawArgs);
    }

    [Theory]
    [InlineData("https://www.github.com/foo", "github.com")]
    [InlineData("https://GITHUB.com/foo", "github.com")]
    [InlineData("http://docs.microsoft.com:8080/x", "docs.microsoft.com")]
    [InlineData("https://wwwexample.com/", "wwwexample.com")]
    public void Host_drops_a_leading_www(string url, string host)
    {
        Assert.Equal(host, LaunchRequest.FromCommandLine($"x.exe {url}", [url]).Host);
    }

    [Theory]
    [InlineData(@"C:\Users\me\page.html")]
    [InlineData("not a url")]
    public void Non_web_arguments_have_no_host(string arg)
    {
        var r = LaunchRequest.FromCommandLine($"x.exe \"{arg}\"", [arg]);

        Assert.Null(r.Host);
        Assert.Equal(arg, r.Url);
    }

    [Fact]
    public void Empty_command_line_yields_an_empty_request()
    {
        var r = LaunchRequest.FromCommandLine("\"C:\\BrowserFork.exe\"", []);

        Assert.Equal("", r.RawArgs);
        Assert.Null(r.Url);
        Assert.Null(r.Host);
    }
}
