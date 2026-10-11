using System.Diagnostics;

namespace Aw.Sindri.Tests.Context;

/// <summary>A throwaway directory, optionally a git repo with every file added, deleted on dispose.</summary>
public sealed class TempTree : IDisposable
{
    public TempTree()
    {
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aw-sindri-ctx-" + System.IO.Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    /// <summary>Writes <paramref name="text"/> at <paramref name="relative"/>, making directories.</summary>
    public string Write(string relative, string text)
    {
        var full = System.IO.Path.Combine(Path, relative);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);   // why: full is under Path, so it has a directory
        File.WriteAllText(full, text);
        return full;
    }

    /// <summary>Makes the directory a git repo and adds every file to the index (git grep reads tracked files).</summary>
    public void GitAddAll()
    {
        Git("init", "-q");
        Git("add", "-A");
    }

    private void Git(params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(Path);
        foreach (var a in args)
        {
            start.ArgumentList.Add(a);
        }

        using var git = Process.Start(start)!;   // why: Start returns a process for a non-shell start
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);
    }
}
