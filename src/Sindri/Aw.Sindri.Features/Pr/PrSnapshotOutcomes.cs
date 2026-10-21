namespace Aw.Sindri.Features.Pr;

/// <summary>The outcomes of 'step.pr.snapshot' (PR babysitter spec §4.3, the lite slice): the names the recipe routes on.</summary>
internal static class PrSnapshotOutcomes
{
    /// <summary>The pull request is merged.</summary>
    public const string Merged = "merged";

    /// <summary>The pull request is closed unmerged.</summary>
    public const string Closed = "closed";

    /// <summary>A required check failed on the head; the CI-red autofix loop takes it.</summary>
    public const string CiRed = "ciRed";

    /// <summary>A conflict, changes requested or unresolved threads: a human's.</summary>
    public const string Attention = "attention";

    /// <summary>Checks are still running, have not started, or ran on an older head.</summary>
    public const string Pending = "pending";

    /// <summary>The required checks passed on the head.</summary>
    public const string Green = "green";
}
