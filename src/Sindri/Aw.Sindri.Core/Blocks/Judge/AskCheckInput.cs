using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>judge ask-check: may the agent continue without asking the human?</summary>
/// <param name="TranscriptTail">The end of the transcript.</param>
public sealed record AskCheckInput(string TranscriptTail) : JudgeInput
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Question => "ask-check";
}
