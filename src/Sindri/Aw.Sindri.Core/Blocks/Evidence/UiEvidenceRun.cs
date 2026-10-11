using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>What a ui-evidence run or parity measurement takes.</summary>
/// <param name="Input">The UI script (run) or design manifest (parity).</param>
/// <param name="RunDirectory">The run directory the document is written to.</param>
/// <param name="Host">The app's origin (ui-evidence's host guard decides which it drives).</param>
/// <param name="AppBuild">The commit under test, recorded in the document.</param>
public sealed record UiEvidenceRun(string Input, string RunDirectory, string Host, HeadSha AppBuild);
