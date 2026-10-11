using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.ShowWorkflow;

internal sealed class ShowWorkflowService(IWorkflowStore store, IRecipeCatalog catalog) : IService<ShowWorkflowRequest, ShowWorkflowResponse>
{
    public async ValueTask<Result<ShowWorkflowResponse>> Handle(ShowWorkflowRequest request, CancellationToken cancellationToken)
    {
        var found = await store.Get(request.Instance, cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value is not { } instance)
        {
            return found.IsError ? found.Error : SindriErrors.InstanceUnknown.With($"no workflow instance {request.Instance.Value} (fix: sindri workflow list)");
        }

        var children = await store.Children(instance.Id, cancellationToken).ConfigureAwait(false);
        var history = await store.History(instance.Id, cancellationToken).ConfigureAwait(false);
        if (children.IsError || history.IsError)
        {
            return children.IsError ? children.Error : history.Error;
        }

        var recipe = await catalog.Get(instance.Recipe, cancellationToken).ConfigureAwait(false);
        var current = recipe.IsSuccess && recipe.Value.Hash == instance.RecipeHash ? recipe.Value.Recipe : null;
        return Describe(instance, current, children.Value, history.Value);
    }

    private static ShowWorkflowResponse Describe(WorkflowInstance instance, Recipe? recipe, IReadOnlyList<WorkflowInstance> children, IReadOnlyList<Core.Ledger.LedgerEvent> history)
    {
        var state = recipe?.State(instance.State);
        var signals = recipe?.Transitions.Where(t => t.From == instance.State).Select(t => t.On).Distinct(StringComparer.Ordinal).ToList() ?? [];
        var entries = history.Select(e => new HistoryEntry(e.Sequence, e.Envelope.Type, e.Envelope.OccurredAt, e.Epoch, e.IdempotencyKey)).ToList();
        return new ShowWorkflowResponse(instance, WorkflowWords.Status(instance), state?.Kind, state?.Next, signals, children, entries);
    }
}
