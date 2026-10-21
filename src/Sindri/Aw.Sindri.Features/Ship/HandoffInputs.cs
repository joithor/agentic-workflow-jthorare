using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Ship;

/// <summary>Everything the hand-off packet's files are generated from.</summary>
/// <param name="Sources">What the phase, proof and review packets say.</param>
/// <param name="Line">Where the phase sits in its line.</param>
/// <param name="Address">The instance's address.</param>
/// <param name="Parents">The phase packet's, proof pack's and review result's hashes, in that order.</param>
/// <param name="Green">The GREEN gate's fact.</param>
internal sealed record HandoffInputs(PackSources Sources, HandoffLine Line, string Address, IReadOnlyList<string> Parents, JsonObject Green);
