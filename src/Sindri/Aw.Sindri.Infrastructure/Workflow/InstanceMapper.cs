using System.Text.Json;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a workflow_instances row's snapshot_json (the first column) back to the instance through AwJson.</summary>
/// <param name="options">The package's AwJson options.</param>
internal sealed class InstanceMapper(JsonSerializerOptions options) : IRowMapper<WorkflowInstance>
{
    public WorkflowInstance Map(SqliteDataReader reader)
    {
        var read = AwJson.Deserialize<WorkflowInstance>(System.Text.Encoding.UTF8.GetBytes(reader.GetString(0)), options);
        return read.IsSuccess ? read.Value : throw new InvalidDataException($"A workflow_instances row doesn't read back: {read.Error.Message}");
    }
}
