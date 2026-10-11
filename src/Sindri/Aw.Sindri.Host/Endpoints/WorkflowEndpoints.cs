using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.CancelWorkflow;
using Aw.Sindri.Features.CollectEvidence;
using Aw.Sindri.Features.HandBackWorkflow;
using Aw.Sindri.Features.ListBlocks;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.ParkWorkflow;
using Aw.Sindri.Features.RegisterEvidence;
using Aw.Sindri.Features.ShowWorkflow;
using Aw.Sindri.Features.SignalWorkflow;
using Aw.Sindri.Features.StartWorkflow;
using Aw.Sindri.Features.TakeOverWorkflow;
using Aw.Sindri.Features.UnparkWorkflow;
using Aw.Sindri.Host.Bodies;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The workflow engine's routes (factory addendum §17.28, §17.27.1): each binds, calls one service and maps its Result.</summary>
internal static class WorkflowEndpoints
{
    internal static RouteGroupBuilder MapWorkflowEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflows", static async (bool? all, IService<ListWorkflowsRequest, ListWorkflowsResponse> service, CancellationToken ct)
            => (await service.Handle(new ListWorkflowsRequest(all ?? false), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/workflows/{instance}", static async (InstanceId instance, IService<ShowWorkflowRequest, ShowWorkflowResponse> service, CancellationToken ct)
            => (await service.Handle(new ShowWorkflowRequest(instance), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows", static async (StartWorkflowRequest request, IService<StartWorkflowRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/signal", static async (InstanceId instance, SignalBody b, IService<SignalWorkflowRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(new SignalWorkflowRequest(instance, b.On, b.ExpectedVersion, b.Sha, b.Outputs, b.Findings, b.EmptyDiff, b.Tokens, b.Turns), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/cancel", static async (InstanceId instance, ReasonBody? b, IService<CancelWorkflowRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(new CancelWorkflowRequest(instance, b?.Reason), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/park", static async (InstanceId instance, ParkBody b, IService<ParkWorkflowRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(new ParkWorkflowRequest(instance, b.Reason, b.Detail), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/unpark", static async (InstanceId instance, BudgetBody? b, IService<UnparkWorkflowRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(new UnparkWorkflowRequest(instance, b?.Tokens ?? 0, b?.Turns ?? 0), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/takeover", static async (InstanceId instance, IService<TakeOverWorkflowRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(new TakeOverWorkflowRequest(instance), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/handback", static async (InstanceId instance, IService<HandBackWorkflowRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(new HandBackWorkflowRequest(instance), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/evidence", static async (InstanceId instance, EvidenceBody b, IService<RegisterEvidenceRequest, WorkflowOutcome> service, CancellationToken ct)
            => (await service.Handle(new RegisterEvidenceRequest(instance, b.Kind, b.Sha, b.Digest), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/workflows/{instance}/evidence/collect", static async (InstanceId instance, CollectBody b, IService<CollectEvidenceRequest, CollectEvidenceResponse> service, CancellationToken ct)
            => (await service.Handle(new CollectEvidenceRequest(instance, b.Kind, b.Sha, b.Inputs ?? new Dictionary<string, string>(StringComparer.Ordinal)), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/recipes", static async (IService<ListRecipesRequest, ListRecipesResponse> service, CancellationToken ct)
            => (await service.Handle(new ListRecipesRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/blocks", static async (IService<ListBlocksRequest, ListBlocksResponse> service, CancellationToken ct)
            => (await service.Handle(new ListBlocksRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
