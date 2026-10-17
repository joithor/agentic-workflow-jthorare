using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxOpen;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Host.Onyx;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri onyx up' and 'sindri onyx down': they run Docker and write Onyx's files under the Sindri home, so only a human at a terminal runs them (an agent
/// session or a non-terminal gets SND-AUTH-002, exit 1, like 'mcp register --apply'). Local: they run in this process, need no control plane, and change
/// nothing for a refused caller. Progress is on standard error (lines and dots, through IOnyxProgress); standard output carries only the summary.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="session">Who runs the CLI.</param>
/// <param name="approver">Approves the profile at the terminal once `onyx up` has written it.</param>
internal sealed class OnyxVerbs(IServiceProvider services, Output output, AgentSession session, IProfileApprover approver)
{
    internal async Task<int> Up(bool json, CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("up", "runs Docker and writes Onyx's files").ConfigureAwait(false);
        }

        Result<OnyxUpResponse> answer;
        try
        {
            answer = await services.GetRequiredService<IService<OnyxUpRequest, OnyxUpResponse>>().Handle(new OnyxUpRequest(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The runner killed the docker child, the services released the lock and removed their temporary files on the way out; each step is recorded only once done.
            return Interrupted("interrupted; run `sindri onyx up` again to resume");
        }

        if (answer.IsError)
        {
            return await Fail(output, answer.Error).ConfigureAwait(false);
        }

        return answer.Value.NeedsHuman.Count > 0 ? await Waiting(answer.Value, json).ConfigureAwait(false) : await HandOver(answer.Value, json, ct).ConfigureAwait(false);
    }

    // A step needs the person (no terminal, end of input, Ctrl-C): say which and what to do; nothing is approved or opened.
    private async Task<int> Waiting(OnyxUpResponse up, bool json)
    {
        var needs = string.Join('\n', up.Steps.Where(static s => s.Outcome == OnyxStepOutcome.NeedsHuman).Select(static s => s.Hint is null ? $"needs you: {s.Step}" : $"needs you: {s.Step} ({s.Hint})"));
        return json ? await output.Json(up, 0).ConfigureAwait(false) : await output.Text($"{Summary(up)}{needs}\n", 0).ConfigureAwait(false);
    }

    // Step 10: approve the profile, read doctor's onyx row, open the connectors page, print what is left.
    private async Task<int> HandOver(OnyxUpResponse up, bool json, CancellationToken ct)
    {
        HandOverResult result;
        try
        {
            result = await new OnyxHandOver(approver, services, services.GetRequiredService<IBrowser>()).Run(up, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Interrupted("interrupted before the profile was approved; run `sindri profile approve` to finish");
        }

        if (result.Failure is { } failed)
        {
            _ = await output.Error(failed).ConfigureAwait(false);
            return 1;
        }

        return json ? await output.Json(result.Response, 0).ConfigureAwait(false) : await output.Text(Summary(result.Response) + result.Text, 0).ConfigureAwait(false);
    }

    private static string Summary(OnyxUpResponse up)
    {
        var rows = new List<string> { $"{"url",-10}{up.Url}", $"{"version",-10}{up.Version}" };
        if (up.Key is not null)
        {
            rows.Add($"{"key",-10}{up.Key}");
        }

        if (up.Profile is not null)
        {
            rows.Add($"{"profile",-10}{(up.Profile == "approved" ? "approved" : "written, not approved (approve later: sindri profile approve)")}");
            rows.Add($"{"doctor",-10}{(up.Doctor is { } check ? $"{DoctorWord(check.State)} {check.Detail}" : "not checked")}");
        }

        rows.Add($"{"ran",-10}{Steps(up.Ran)}");
        rows.Add($"{"skipped",-10}{Steps(up.Skipped)}");
        return string.Join('\n', rows) + "\n";
    }

    private static string DoctorWord(Aw.Sindri.Core.Doctor.CheckState state) => state switch
    {
        Aw.Sindri.Core.Doctor.CheckState.Ok => "ok",
        Aw.Sindri.Core.Doctor.CheckState.Warn => "warn",
        Aw.Sindri.Core.Doctor.CheckState.Fail => "fail",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "unknown check state"),
    };

    internal Task<int> Open(bool json, CancellationToken ct)
    {
        _ = (json, ct, typeof(OnyxOpenRequest), session);
        throw new NotSupportedException();
    }

    internal async Task<int> Down(bool json, CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("down", "stops Docker containers").ConfigureAwait(false);
        }

        Result<OnyxDownResponse> answer;
        try
        {
            answer = await services.GetRequiredService<IService<OnyxDownRequest, OnyxDownResponse>>().Handle(new OnyxDownRequest(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Interrupted("interrupted; run `sindri onyx down` again to finish stopping Onyx");
        }

        if (answer.IsError)
        {
            return await Fail(output, answer.Error).ConfigureAwait(false);
        }

        var progress = services.GetRequiredService<IOnyxProgress>();
        foreach (var warning in answer.Value.Warnings)
        {
            progress.Line(warning);
        }

        return json
            ? await output.Json(answer.Value, 0).ConfigureAwait(false)
            : await output.Text(answer.Value.WasRunning
                ? $"Onyx stopped: {string.Join(", ", answer.Value.Stopped)} (its data and images are kept; `sindri onyx up` starts it again).\n"
                : "Onyx is stopped (nothing was running).\n", 0).ConfigureAwait(false);
    }

    /// <summary>Prints an error the way the onyx verbs exit: 1 for an SND-ONYX error or a busy command (attention: the message names the fix), 2 for anything else.</summary>
    /// <param name="output">Where the error goes.</param>
    /// <param name="error">The error.</param>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Fail(Output output, Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return error.Code.StartsWith("SND-ONYX-", StringComparison.Ordinal) || error.Code == SindriErrors.CommandBusy.Code ? 1 : 2;
    }

    private static string Steps(IReadOnlyList<string> steps) => steps.Count == 0 ? "none" : string.Join(", ", steps);

    // Through the progress writer, which ends an open row of dots before the line, so the resume line never starts mid-row.
    private int Interrupted(string text)
    {
        services.GetRequiredService<IOnyxProgress>().Line(text);
        return Output.InterruptedExit;
    }

    // The printed refusal: SND-AUTH-002, exit 1 (attention, not a tool error), with nothing called.
    private async Task<int> Refused(string verb, string what)
    {
        _ = await output.Error(SindriErrors.AuthRefused.With(
            $"`sindri onyx {verb}` {what}, so only a human at a terminal runs it (this CLI ran as a session: {session.SessionReason}; fix: ask the human to run `sindri onyx {verb}`)")).ConfigureAwait(false);
        return 1;
    }
}
