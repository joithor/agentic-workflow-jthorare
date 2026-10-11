using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>
/// A scripted stand-in for a tool binary (judge, ui-evidence) in a temp directory: a /bin/sh script that answers from
/// files beside it, so the process adapters run against a real process with a known answer.
/// </summary>
internal sealed class StubTool : IDisposable
{
    private readonly TempHome _home = new();

    public StubTool(string script)
    {
        Binary = Path.Combine(_home.Path, "tool");
        File.WriteAllText(Binary, "#!/bin/sh\nd=$(dirname \"$0\")\n" + script);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public string Binary { get; }

    public string Directory => _home.Path;

    public void Write(string name, string text) => File.WriteAllText(Path.Combine(_home.Path, name), text);

    public string Read(string name) => File.ReadAllText(Path.Combine(_home.Path, name));

    public void Dispose() => _home.Dispose();
}
