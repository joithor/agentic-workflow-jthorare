using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Recipes;

/// <summary>A named loop counter (review rounds, fix rounds, revise rounds): past its cap the instance parks with the reason 'rounds'.</summary>
/// <param name="Cap">The most rounds, 1 to 50.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoopSpec(int Cap);
