using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.CollectEvidence;
using Aw.Sindri.Features.ListBlocks;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.ShowWorkflow;
using Aw.Sindri.Features.StartWorkflow;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Bodies;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow …': every verb but 'validate' is a client of the running control plane's API (§17.27.1); 'validate'
/// runs the validator in this process, read-only, so it works while the control plane is down.
/// </summary>
internal sealed class WorkflowVerbs(ControlPlaneClient client, Output output, IServiceProvider scope)
{
    internal async Task<int> Run(IReadOnlyList<string> args, CancellationToken ct)
    {
        var (parsed, problem) = WorkflowArgs.Parse(args);
        if (parsed is null || parsed.Positionals.Count == 0)
        {
            return await Usage(problem ?? "a workflow verb is needed").ConfigureAwait(false);
        }

        var p = parsed.Positionals;
        return (p[0], p.Count) switch
        {
            ("list", 1) => await Read<ListWorkflowsResponse>($"/sindri/workflows?all={(parsed.Flag("--all") ? "true" : "false")}", parsed, r => WorkflowText.List(r.Instances), ct).ConfigureAwait(false),
            ("show", 2) => await WithId(p[1], id => Read<ShowWorkflowResponse>($"/sindri/workflows/{id.Value}", parsed, WorkflowText.Show, ct)).ConfigureAwait(false),
            ("start", 3) => await Start(p[1], p[2], parsed, ct).ConfigureAwait(false),
            ("recipes", 1) => await Read<ListRecipesResponse>("/sindri/recipes", parsed, r => WorkflowText.Recipes(r.Recipes), ct).ConfigureAwait(false),
            ("blocks", 1) => await Read<ListBlocksResponse>("/sindri/blocks", parsed, r => BlockText.Render(r.Blocks), ct).ConfigureAwait(false),
            ("validate", 2) => await Validate(p[1], parsed, ct).ConfigureAwait(false),
            _ => await new WorkflowCommands(this, parsed).Run(ct).ConfigureAwait(false),
        };
    }

    internal async Task<int> Mutate<TBody>(string route, TBody? body, WorkflowArgs args, CancellationToken ct)
    {
        var answer = await client.Post<TBody, WorkflowOutcome>(route, body, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Instance.Status is InstanceStatus.Parked or InstanceStatus.Escalated ? 1 : 0;
        return args.Json ? await output.Json(answer.Value, code).ConfigureAwait(false) : await output.Text(WorkflowText.Outcome(answer.Value), code).ConfigureAwait(false);
    }

    internal async Task<int> Collect(string route, CollectBody body, WorkflowArgs args, CancellationToken ct)
    {
        var answer = await client.Post<CollectBody, CollectEvidenceResponse>(route, body, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Registered is { Instance.Status: not (InstanceStatus.Parked or InstanceStatus.Escalated) } ? 0 : 1;
        return args.Json ? await output.Json(answer.Value, code).ConfigureAwait(false) : await output.Text(WorkflowText.Collected(answer.Value), code).ConfigureAwait(false);
    }

    internal async Task<int> WithId(string text, Func<InstanceId, Task<int>> next) =>
        InstanceId.TryFrom(text, out var id)
            ? await next(id).ConfigureAwait(false)
            : await output.Error(SindriErrors.InstanceUnknown.With($"'{text}' is not an instance id ('wf-' and 12 hex digits; fix: sindri workflow list)")).ConfigureAwait(false);

    internal async Task<int> Usage(string problem)
    {
        _ = await output.Error(SindriErrors.UnknownVerb.With($"workflow: {problem}")).ConfigureAwait(false);
        await output.Out.FlushAsync().ConfigureAwait(false);
        await Console.Error.WriteLineAsync(Commands.Usage.Workflow).ConfigureAwait(false);
        return 2;
    }

    private async Task<int> Read<T>(string route, WorkflowArgs args, Func<T, string> text, CancellationToken ct)
    {
        var answer = await client.Get<T>(route, ct).ConfigureAwait(false);
        return answer.IsError
            ? await output.Error(answer.Error).ConfigureAwait(false)
            : args.Json ? await output.Json(answer.Value, 0).ConfigureAwait(false) : await output.Text(text(answer.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Start(string recipeText, string itemText, WorkflowArgs args, CancellationToken ct)
    {
        if (!RecipeId.TryFrom(recipeText, out var recipe) || !ItemId.TryFrom(itemText, out var item) || args.BadNumber("--attempt"))
        {
            return await Usage($"start <recipe> <item> [--attempt N] [--base SHA]: '{recipeText}' or '{itemText}' or the attempt isn't valid").ConfigureAwait(false);
        }

        HeadSha? baseSha = args.Option("--base") is { } b && HeadSha.TryFrom(b, out var sha) ? sha : null;
        var attempt = (int)Math.Clamp(args.Number("--attempt") ?? 1, 0, int.MaxValue);
        return await Mutate("/sindri/workflows", new StartWorkflowRequest(recipe, item, attempt, baseSha), args, ct).ConfigureAwait(false);
    }

    private async Task<int> Validate(string path, WorkflowArgs args, CancellationToken ct)
    {
        var service = scope.GetRequiredService<IService<ValidateRecipeRequest, CatalogEntry>>();
        var entry = await service.Handle(new ValidateRecipeRequest(Path.GetFullPath(path)), ct).ConfigureAwait(false);
        if (entry.IsError)
        {
            return await output.Error(entry.Error).ConfigureAwait(false);
        }

        var code = entry.Value.Valid ? 0 : 2;
        return args.Json ? await output.Json(entry.Value, code).ConfigureAwait(false) : await output.Text(WorkflowText.Validation(entry.Value), code).ConfigureAwait(false);
    }
}
