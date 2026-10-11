namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The workflow store's SQL (migration 0004). Engine-specific forms (ON CONFLICT upserts, RETURNING) stay here, named.</summary>
internal static class WorkflowSql
{
    internal const string SelectByKey = "SELECT seq FROM events WHERE idempotency_key = $key";

    internal const string SelectVersion = "SELECT version FROM workflow_instances WHERE id = $id";

    internal const string InsertEvent =
        "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, item, correlation_id, causation_id, " +
        "trace_id, epoch, idempotency_key, payload_json, instance) VALUES ($id, $type, $schemaVersion, $occurredAt, $actor, $tenant, " +
        "$ring, $item, $correlationId, $causationId, $traceId, $epoch, $key, $payload, $instance) RETURNING seq";

    internal const string UpsertInstance =
        "INSERT INTO workflow_instances (id, recipe, recipe_hash, item, attempt, parent, tenant, ring, version, state, status, deadline, " +
        "started_at, updated_at, snapshot_json) VALUES ($id, $recipe, $recipeHash, $item, $attempt, $parent, $tenant, $ring, $version, " +
        "$state, $status, $deadline, $startedAt, $updatedAt, $snapshot) ON CONFLICT (id) DO UPDATE SET version = excluded.version, " +
        "state = excluded.state, status = excluded.status, deadline = excluded.deadline, updated_at = excluded.updated_at, " +
        "snapshot_json = excluded.snapshot_json";

    internal const string SelectInstance = "SELECT snapshot_json FROM workflow_instances WHERE id = $id";

    internal const string SelectList =
        "SELECT snapshot_json FROM workflow_instances WHERE $all = 1 OR status NOT IN ('done', 'cancelled') " +
        "ORDER BY updated_at DESC, id LIMIT $max";

    internal const string SelectDue =
        "SELECT snapshot_json FROM workflow_instances WHERE deadline IS NOT NULL AND deadline <= $now AND status = 'active' " +
        "ORDER BY deadline, id LIMIT $max";

    internal const string SelectChildren = "SELECT snapshot_json FROM workflow_instances WHERE parent = $id ORDER BY started_at, id";

    internal const string SelectHistory =
        "SELECT seq, epoch, id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, causation_id, trace_id, " +
        "payload_json, idempotency_key, item FROM events WHERE instance = $id ORDER BY seq";
}
