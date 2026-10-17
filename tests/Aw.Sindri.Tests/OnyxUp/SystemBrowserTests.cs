using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

[Lane(TestLane.Unit)]
public sealed class SystemBrowserTests
{
    private static readonly ToolRun _exitedOk = new(ToolOutcome.Exited, 0, string.Empty);

    [Theory]
    [InlineData(true, "open")]
    [InlineData(false, "xdg-open")]
    public void Browser_uses_open_on_macOS_and_xdg_open_on_Linux_with_the_url_as_the_only_argument(bool macOS, string tool)
    {
        var runner = new FakeToolRunner(_exitedOk);

        var opened = new SystemBrowser(runner, macOS ? DesktopOs.MacOS : DesktopOs.Linux).Open(OnyxPages.SignUp(3002));

        Assert.True(opened);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(tool, call.FileName);
        Assert.Equal(["http://127.0.0.1:3002/auth/create-account"], call.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(5), call.Timeout);
    }

    [Fact]
    public void A_missing_browser_tool_returns_false()
        => Assert.False(new SystemBrowser(new FakeToolRunner(ToolRun.Missing), DesktopOs.Linux).Open(OnyxPages.SignUp(3000)));

    [Fact]
    public void A_failing_tool_returns_false()
        => Assert.False(new SystemBrowser(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, string.Empty)), DesktopOs.MacOS).Open(OnyxPages.SignUp(3000)));

    [Fact]
    public void An_unsupported_desktop_runs_nothing_and_returns_false()
    {
        var runner = new FakeToolRunner(_exitedOk);

        Assert.False(new SystemBrowser(runner, DesktopOs.Unsupported).Open(OnyxPages.SignUp(3000)));
        Assert.Empty(runner.Calls);
    }

    [Theory]
    [InlineData("file:///etc/hosts")]
    [InlineData("ftp://127.0.0.1/x")]
    [InlineData("mailto:someone@example.com")]
    public void Only_http_addresses_are_opened(string url)
    {
        var runner = new FakeToolRunner(_exitedOk);

        Assert.False(new SystemBrowser(runner, DesktopOs.MacOS).Open(new Uri(url)));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void The_pages_are_on_loopback_with_the_given_port()
    {
        Assert.Equal("http://127.0.0.1:3004/auth/create-account", OnyxPages.SignUp(3004).AbsoluteUri);
        Assert.Equal("http://127.0.0.1:3004/admin/connectors", OnyxPages.Connectors(3004).AbsoluteUri);
    }
}
