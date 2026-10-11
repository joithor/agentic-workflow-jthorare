using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>A judge question's input record, written to the judge's stdin; <see cref="Question"/> names the question and isn't written.</summary>
public abstract record JudgeInput
{
    /// <summary>The question, the first word of the judge command line.</summary>
    [JsonIgnore]
    public abstract string Question { get; }
}
