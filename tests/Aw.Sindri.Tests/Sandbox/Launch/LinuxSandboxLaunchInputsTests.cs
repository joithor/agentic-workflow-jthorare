using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The Linux sandbox step's box facts: paths, ports, host facts from the machine port, git's common directory, and the short socket directory.</summary>
[Lane(TestLane.Unit)]
public sealed class LinuxSandboxLaunchInputsTests : IDisposable
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-0123456789abcdef0123456789abcdef");

    private readonly TempDirectory _temp = new();
    private readonly FakeProcesses _processes = new();

    public void Dispose() => _temp.Dispose();

    private LinuxSandboxLaunchInputs Make(ControlEndpoint? control = null) =>
        new(new LauncherOptions(_temp.Path) { SindriHome = "/real/home" }, new AgentCatalog([AgentEntries.ClaudeCode]), new Machine(), _processes,
            () => new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = "/home/owner", ["AW_STATE_DIR"] = "/state" }, () => control);

    [Fact]
    public void Host_takes_memory_and_cores_from_the_machine_and_the_homes_from_the_environment()
    {
        var host = Make().Host();

        Expect.Equal(8L * 1024 * 1024 * 1024, host.TotalMemoryBytes);
        Expect.Equal(6, host.CpuCores);
        Expect.Equal("/home/owner", host.Home);
        Expect.Equal("/state", host.StateDir);
        Expect.Equal("/real/home", host.SindriHome);
        Expect.Equal(string.Empty, host.CsBinDir);   // no bin/cs/current under /state
    }

    [Fact]
    public void The_sindri_port_is_the_control_plane_s_and_the_proxy_port_is_fixed()
    {
        var inputs = Make(new ControlEndpoint(new Uri("http://127.0.0.1:7480/"), Pid: 1, Epoch: 1));

        Expect.Equal(7480, inputs.SindriPort());
        Expect.Equal(3128, inputs.ProxyPort());
        Expect.Equal(0, Make().SindriPort());
    }

    [Fact]
    public void The_policy_form_comes_from_the_catalog_entry_and_is_none_for_an_unknown_agent()
    {
        var inputs = Make();

        Expect.Equal(AgentEntries.ClaudeCode.PolicyForm, inputs.PolicyFormOf(AgentEntries.ClaudeCode.Id));
        Expect.Equal(PolicyForm.None, inputs.PolicyFormOf(AgentId.From("nobody")));
    }

    [Fact]
    public async Task The_git_common_directory_is_what_git_prints_made_absolute_or_dot_git_when_git_fails()
    {
        _ = _processes.When((_, a) => a.Contains("--git-common-dir"), FakeProcesses.Ok("../.git\n"));

        Expect.Equal("/work/.git", await Make().GitCommonDir("/work/web", CancellationToken.None));
        Expect.Equal("/other/.git", await new LinuxSandboxLaunchInputs(new LauncherOptions(_temp.Path), new AgentCatalog([]), new Machine(), new FakeProcesses(), () => new Dictionary<string, string>(StringComparer.Ordinal), () => null).GitCommonDir("/other", CancellationToken.None));
    }

    [Fact]
    public void The_net_directory_is_short_enough_for_a_socket_path()
    {
        var dir = LinuxSandboxLaunchInputs.NetDir("/run/user/1000", _launch);

        Expect.Equal("/run/user/1000/aw-net/0123456789ab", dir);
        Expect.True(dir.Length + "/sindri.sock".Length < 100);
    }

    [Fact]
    public void Other_homes_are_the_other_directories_under_the_root_sorted()
    {
        foreach (var name in new[] { "zed", "owner", "amy" })
        {
            _ = Directory.CreateDirectory(Path.Combine(_temp.Path, name));
        }

        var others = LinuxSandboxLaunchInputs.OtherHomes(_temp.Path, Path.Combine(_temp.Path, "owner"));

        Expect.True(others.SequenceEqual([Path.Combine(_temp.Path, "amy"), Path.Combine(_temp.Path, "zed")], StringComparer.Ordinal));
    }

    [Fact]
    public void The_run_area_is_per_launch_under_the_launcher_directory()
    {
        Expect.Equal(Path.Combine(_temp.Path, "launch", "areas", _launch.Value), Make().RunAreaFor(_launch));
    }

    private sealed class Machine : ISandboxMachine
    {
        public long RamBytes => 8L * 1024 * 1024 * 1024;

        public int Cores => 6;
    }
}
