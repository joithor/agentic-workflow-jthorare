namespace Aw.Sindri.Core.Blocks;

/// <summary>The outbox command kinds an effect may emit (§17.28.5: effects only use allowlisted command kinds). The dispatcher knows exactly these.</summary>
public static class EffectAllowlist
{
    /// <summary>Tell the human something (a notification within the budget, §10.1).</summary>
    public const string NotifyHuman = "notify.human";

    /// <summary>Open or update a draft pull request.</summary>
    public const string DraftPullRequest = "pr.draft";

    /// <summary>Every allowlisted kind.</summary>
    public static IReadOnlySet<string> Kinds { get; } = new HashSet<string>(StringComparer.Ordinal) { NotifyHuman, DraftPullRequest };
}
