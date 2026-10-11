using Aw.Kernel.Data;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The bound statements for one instance write: its event row (with the instance column) and its instance row.</summary>
internal static class WorkflowRows
{
    internal static SqlStatement Event(InstanceWrite write, LeaseGrant grant)
    {
        var (e, entry) = (write.Event.Envelope, write.Event);
        return new SqlStatement(WorkflowSql.InsertEvent, c => c
            .Bind("$id", e.Id).Bind("$type", e.Type).Bind("$schemaVersion", e.SchemaVersion).Bind("$occurredAt", e.OccurredAt)
            .Bind("$actor", e.Actor).Bind("$tenant", e.Tenant).Bind("$ring", e.Ring).Bind("$item", entry.Item?.Value)
            .Bind("$correlationId", e.CorrelationId).Bind("$causationId", e.CausationId?.ToString("D"))
            .Bind("$traceId", e.TraceId).Bind("$epoch", grant.Epoch).Bind("$key", entry.IdempotencyKey).Bind("$payload", e.Payload)
            .Bind("$instance", write.After.Id.Value), $"event {e.Type}");
    }

    internal static SqlStatement Instance(WorkflowInstance i, string snapshot) => new(WorkflowSql.UpsertInstance, c => c
        .Bind("$id", i.Id.Value).Bind("$recipe", i.Recipe.Value).Bind("$recipeHash", i.RecipeHash.Value).Bind("$item", i.Item.Value)
        .Bind("$attempt", i.Attempt).Bind("$parent", i.Parent?.Value).Bind("$tenant", i.Tenant).Bind("$ring", i.Ring)
        .Bind("$version", i.Version).Bind("$state", i.State).Bind("$status", StatusWord.Of(i.Status))
        .Bind("$startedAt", i.StartedAt).Bind("$updatedAt", i.UpdatedAt).Bind("$snapshot", snapshot).BindDeadline(i.Deadline), $"workflow instance {i.Id.Value}");

    // A running timer is bound as the same UTC text every timestamp is (so it sorts and compares as text); none is NULL.
    private static Microsoft.Data.Sqlite.SqliteCommand BindDeadline(this Microsoft.Data.Sqlite.SqliteCommand command, DateTimeOffset? deadline) =>
        deadline is { } d ? command.Bind("$deadline", d) : command.Bind("$deadline", (string?)null);
}
