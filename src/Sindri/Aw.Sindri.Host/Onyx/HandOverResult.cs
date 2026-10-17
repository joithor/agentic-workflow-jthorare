using Aw.Kernel;
using Aw.Sindri.Features.OnyxUp;

namespace Aw.Sindri.Host.Onyx;

/// <summary>What the hand-over ended with.</summary>
/// <param name="Response">The up response with the profile, doctor and connectors filled in.</param>
/// <param name="Text">The lines the terminal gets after the summary (the connectors page and the checklist).</param>
/// <param name="Failure">The approval's error when it failed for a reason other than the human declining; null otherwise.</param>
internal sealed record HandOverResult(OnyxUpResponse Response, string Text, Error? Failure);
