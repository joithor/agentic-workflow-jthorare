using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Sindri.Core.Blocks.Builtin;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.CollectEvidence;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListBlocks;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ShowWorkflow;
using Aw.Sindri.Features.StartWorkflow;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Host.Bodies;

namespace Aw.Sindri.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(SindriProfile))]
[JsonSerializable(typeof(ControlEndpoint))]
[JsonSerializable(typeof(Ping))]
[JsonSerializable(typeof(ApiProblem))]
[JsonSerializable(typeof(ProcessStarted))]
[JsonSerializable(typeof(ProcessStopped))]
[JsonSerializable(typeof(ProfileApproved))]
[JsonSerializable(typeof(Heartbeat))]
[JsonSerializable(typeof(GetStatusResponse))]
[JsonSerializable(typeof(StopProcessResponse))]
[JsonSerializable(typeof(ApproveProfileRequest))]
[JsonSerializable(typeof(ApproveProfileResponse))]
[JsonSerializable(typeof(ValidateProfileResponse))]
[JsonSerializable(typeof(RunDoctorResponse))]
[JsonSerializable(typeof(EventView))]
[JsonSerializable(typeof(WorkflowOutcome))]
[JsonSerializable(typeof(WorkflowInstance))]
[JsonSerializable(typeof(WorkflowChange))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(CatalogEntry))]
[JsonSerializable(typeof(NotifyHuman))]
[JsonSerializable(typeof(DraftPullRequest))]
[JsonSerializable(typeof(TickReport))]
[JsonSerializable(typeof(StartWorkflowRequest))]
[JsonSerializable(typeof(ListWorkflowsResponse))]
[JsonSerializable(typeof(ShowWorkflowResponse))]
[JsonSerializable(typeof(ListRecipesResponse))]
[JsonSerializable(typeof(ListBlocksResponse))]
[JsonSerializable(typeof(SignalBody))]
[JsonSerializable(typeof(ReasonBody))]
[JsonSerializable(typeof(ParkBody))]
[JsonSerializable(typeof(BudgetBody))]
[JsonSerializable(typeof(EvidenceBody))]
[JsonSerializable(typeof(CollectBody))]
[JsonSerializable(typeof(CollectEvidenceResponse))]
[JsonSerializable(typeof(JudgeAnswer))]
[JsonSerializable(typeof(ResolutionCheckInput))]
[JsonSerializable(typeof(RuleCheckInput))]
[JsonSerializable(typeof(TurnProgressInput))]
[JsonSerializable(typeof(BriefScopeInput))]
[JsonSerializable(typeof(VisualCritiqueInput))]
[JsonSerializable(typeof(AskCheckInput))]
[JsonSerializable(typeof(StackHealth))]
[JsonSerializable(typeof(DatabaseProvenance))]
internal sealed partial class SindriJsonContext : JsonSerializerContext;
