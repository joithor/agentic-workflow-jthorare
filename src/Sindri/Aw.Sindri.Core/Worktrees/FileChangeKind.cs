namespace Aw.Sindri.Core.Worktrees;

/// <summary>How a file changed in a range.</summary>
public enum FileChangeKind
{
    /// <summary>The file is new.</summary>
    Added = 1,

    /// <summary>The file's content changed.</summary>
    Modified = 2,

    /// <summary>The file is gone.</summary>
    Deleted = 3,

    /// <summary>The file moved (its path is the new one).</summary>
    Renamed = 4,
}
