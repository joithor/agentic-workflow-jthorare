using Aw.Kernel.Outbox;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A commit being built: the instance writes and outbox commands one command adds up to.</summary>
internal sealed class CommitDraft
{
    internal List<InstanceWrite> Writes { get; } = [];

    internal List<OutboxCommand> Commands { get; } = [];

    /// <summary>The instance as this draft last wrote it, or null when the draft doesn't touch it.</summary>
    internal WorkflowInstance? Latest(Ids.InstanceId id) => Writes.LastOrDefault(w => w.After.Id == id)?.After;

    internal WorkflowCommit Build() => new([.. Writes], [.. Commands]);
}
