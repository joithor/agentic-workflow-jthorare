using Aw.Kernel.Outbox;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A commit being built: the instance writes and outbox commands one command adds up to.</summary>
internal sealed class CommitDraft
{
    internal List<InstanceWrite> Writes { get; } = [];

    internal List<OutboxCommand> Commands { get; } = [];

    internal WorkflowCommit Build() => new([.. Writes], [.. Commands]);
}
