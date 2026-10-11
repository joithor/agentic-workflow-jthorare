using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Judge;

namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>
/// The judge through its CLI contract (docs/csharp/judge.md): 'judge &lt;question&gt;' with the input record on stdin. Exits 0
/// and 2 both carry the envelope on stdout (decided or escalated; ask-check swaps them: 0 is 'ask' or an escalation, 2 is
/// 'continue'), so the envelope's own fields decide. 1 is the judge's error line, 64 a usage error; anything else, or an
/// envelope that doesn't read, is SND-BLK-001.
/// </summary>
/// <param name="settings">Where the judge binary is.</param>
/// <param name="json">The package's AwJson options (the input records and the envelope are in its context).</param>
internal sealed class ProcessJudgeClient(ToolSettings settings, JsonSerializerOptions json) : IJudgeClient
{
    public async ValueTask<Result<JudgeAnswer>> Ask(JudgeInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        // The record's runtime type (AwJson.Serialize<T> takes the static one); every question's record is in the context.
        var stdin = JsonSerializer.SerializeToUtf8Bytes(input, json.GetTypeInfo(input.GetType()));
        var run = await ToolProcess.Run(new ToolCall("judge", settings.JudgeBin, [input.Question], stdin, settings.JudgeTimeout), settings, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        return run.Value.Exit switch
        {
            0 or 2 => Envelope(input.Question, run.Value),
            _ => SindriErrors.ToolUnavailable.With(string.Create(CultureInfo.InvariantCulture,
                $"judge {input.Question} exited {run.Value.Exit}: {run.Value.Message}")),
        };
    }

    private Result<JudgeAnswer> Envelope(string question, ToolRun run)
    {
        var answer = AwJson.Deserialize<JudgeAnswer>(run.Stdout, json);
        if (answer.IsError)
        {
            return SindriErrors.ToolUnavailable.With($"judge {question} printed no envelope: {answer.Error.Message}");
        }

        return answer.Value.Escalated || answer.Value.Decision is { Length: > 0 }
            ? answer
            : SindriErrors.ToolUnavailable.With($"judge {question} printed an envelope with neither a decision nor an escalation");
    }
}
