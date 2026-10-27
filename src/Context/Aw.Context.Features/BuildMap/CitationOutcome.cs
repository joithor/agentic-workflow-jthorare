namespace Aw.Context.Features.BuildMap;

/// <summary>How one cited path fared against the build's path lists (spec 2026-10-27 §7.5).</summary>
internal enum CitationOutcome
{
    /// <summary>It names a file of the record's repo, or else of the one other repo that has it.</summary>
    Resolved,

    /// <summary>No mapped repo has it.</summary>
    Missing,

    /// <summary>Reserved for the report's 'other repo' column; no path lands here now, since the one other repo that has a path resolves it.</summary>
    OtherRepo,

    /// <summary>More than one repo has it and the record's own repo (when it names one) is not among them.</summary>
    Ambiguous,
}
