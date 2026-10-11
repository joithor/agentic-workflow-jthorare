using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.CancelWorkflow;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.HandBackWorkflow;
using Aw.Sindri.Features.ListBlocks;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.ParkWorkflow;
using Aw.Sindri.Features.PingProcess;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.RegisterEvidence;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ShowWorkflow;
using Aw.Sindri.Features.SignalWorkflow;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Features.StartWorkflow;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.TakeOverWorkflow;
using Aw.Sindri.Features.TickWorkflowTimers;
using Aw.Sindri.Features.UnparkWorkflow;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers Aw.Sindri.Features's slices: the command layer every client (CLI, API, UI) calls (factory addendum §17.27.1).</summary>
public static class SindriFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Sindri use case as an IService, and the background services as IScheduledService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IService<GetStatusRequest, GetStatusResponse>, GetStatusService>()
            .AddScoped<IService<StartProcessRequest, StartProcessResponse>, StartProcessService>()
            .AddScoped<IService<StopProcessRequest, StopProcessResponse>, StopProcessService>()
            .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
            .AddScoped<IService<StreamEventsRequest, StreamEventsResponse>, StreamEventsService>()
            .AddScoped<IService<PingProcessRequest, Ping>, PingProcessService>()
            .AddScoped<IService<ValidateProfileRequest, ValidateProfileResponse>, ValidateProfileService>()
            .AddScoped<IService<ApproveProfileRequest, ApproveProfileResponse>, ApproveProfileService>()
            .AddScoped<IService<RunDoctorRequest, RunDoctorResponse>, RunDoctorService>()
            .AddScoped<IService<StartWorkflowRequest, WorkflowOutcome>, StartWorkflowService>()
            .AddScoped<IService<SignalWorkflowRequest, WorkflowOutcome>, SignalWorkflowService>()
            .AddScoped<IService<CancelWorkflowRequest, WorkflowOutcome>, CancelWorkflowService>()
            .AddScoped<IService<ParkWorkflowRequest, WorkflowOutcome>, ParkWorkflowService>()
            .AddScoped<IService<UnparkWorkflowRequest, WorkflowOutcome>, UnparkWorkflowService>()
            .AddScoped<IService<TakeOverWorkflowRequest, WorkflowOutcome>, TakeOverWorkflowService>()
            .AddScoped<IService<HandBackWorkflowRequest, WorkflowOutcome>, HandBackWorkflowService>()
            .AddScoped<IService<RegisterEvidenceRequest, WorkflowOutcome>, RegisterEvidenceService>()
            .AddScoped<IService<ListWorkflowsRequest, ListWorkflowsResponse>, ListWorkflowsService>()
            .AddScoped<IService<ShowWorkflowRequest, ShowWorkflowResponse>, ShowWorkflowService>()
            .AddScoped<IService<TickWorkflowTimersRequest, TickReport>, TickWorkflowTimersService>()
            .AddScoped<IService<ListRecipesRequest, ListRecipesResponse>, ListRecipesService>()
            .AddScoped<IService<ListBlocksRequest, ListBlocksResponse>, ListBlocksService>()
            .AddScoped<IService<ValidateRecipeRequest, CatalogEntry>, ValidateRecipeService>()
            .AddSingleton<TickPace>()
            .AddScoped<IScheduledService, WorkflowTimerService>()
            .AddScoped<IScheduledService, HeartbeatService>();
        return services;
    }
}
