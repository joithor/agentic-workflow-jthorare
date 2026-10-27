namespace Aw.Context.Features.BuildMap;

/// <summary>How one cited path fared against the build's path lists (spec 2026-10-27 §7.5).</summary>
internal enum CitationOutcome
{
    /// <summary>It names a file of the record's repo, or of the one repo that has it when the record names none.</summary>
    Resolved,

    /// <summary>No mapped repo has it.</summary>
    Missing,

    /// <summary>Only another repo than the record's has it.</summary>
    OtherRepo,

    /// <summary>The record names no repo and more than one has it.</summary>
    Ambiguous,
}
