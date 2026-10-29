using Xunit;

namespace BrowserFork.Tests;

public class SplitCommandTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\"",
        @"C:\Program Files\Google\Chrome\Application\chrome.exe", "")]
    [InlineData("\"C:\\Program Files\\Mozilla Firefox\\firefox.exe\" -osint -url \"%1\"",
        @"C:\Program Files\Mozilla Firefox\firefox.exe", "-osint -url")]
    [InlineData("\"C:\\Edge\\msedge.exe\" --single-argument %1",
        @"C:\Edge\msedge.exe", "")]
    [InlineData(@"C:\Program Files\Internet Explorer\iexplore.exe",
        @"C:\Program Files\Internet Explorer\iexplore.exe", "")]
    [InlineData(@"C:\Tools\Browser.EXE --profile work",
        @"C:\Tools\Browser.EXE", "--profile work")]
    public void Splits_exe_from_its_arguments_and_drops_placeholders(string command, string exe, string args)
    {
        var (actualExe, actualArgs) = BrowserCatalog.SplitCommand(command);

        Assert.Equal(exe, actualExe);
        Assert.Equal(args, actualArgs);
    }
}
