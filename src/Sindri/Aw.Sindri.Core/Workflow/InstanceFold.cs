namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Turns a change into the next instance. The engine computes every new instance through it, and replay folds an
/// instance's events through it, so replaying the log reproduces the stored instance (§17.28.5).
/// </summary>
public static class InstanceFold
{
    private static readonly Dictionary<ChangeKind, Func<WorkflowInstance, WorkflowChange, WorkflowInstance>> _apply = new()
    {
        [ChangeKind.Transitioned] = Transitioned,
        [ChangeKind.Evidence] = Registered,
        [ChangeKind.Parked] = Held,
        [ChangeKind.Escalated] = Held,
        [ChangeKind.Unparked] = Unparked,
        [ChangeKind.Cancelled] = Cancelled,
        [ChangeKind.TakenOver] = TakenOver,
        [ChangeKind.HandedBack] = HandedBack,
        [ChangeKind.Retried] = Retried,
        [ChangeKind.DeadlineExtended] = Extended,
    };

    /// <summary>The instance after <paramref name="change"/>.</summary>
    /// <param name="current">The instance before it; null only for 'started'.</param>
    /// <param name="change">The change.</param>
    /// <returns>The next instance.</returns>
    public static WorkflowInstance Apply(WorkflowInstance? current, WorkflowChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Kind is ChangeKind.Started)
        {
            return change.Start ?? throw new InvalidOperationException("A 'started' change carries the new instance");
        }

        var before = current ?? throw new InvalidOperationException($"A '{change.EventType}' change needs the instance it applies to");
        return _apply[change.Kind](before, change) with { Version = change.Version, UpdatedAt = change.At };
    }

    private static WorkflowInstance Transitioned(WorkflowInstance before, WorkflowChange change)
    {
        var to = change.To ?? before.State;
        var spend = change.Spend ?? Spend.Zero;
        var loops = Sorted(before.Loops);
        var findings = Sorted(before.Findings);
        if (change.Loop is { } loop)
        {
            loops[loop] = loops.GetValueOrDefault(loop) + 1;
            findings[loop] = change.Findings ?? [];
        }

        var facts = Sorted(before.Facts);
        foreach (var (name, value) in change.Facts ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            facts[name] = value;
        }

        return before with
        {
            State = to,
            Facts = facts,
            Loops = loops,
            Findings = findings,
            Spent = before.Spent.Plus(spend),
            StateSpent = Spend.Zero,
            Deadline = change.Deadline,
            Retries = 0,
            EnteredAt = change.At,
            Ended = change.Completed ? InstanceEnd.Completed : null,
        };
    }

    private static WorkflowInstance Held(WorkflowInstance before, WorkflowChange change) => before with
    {
        Hold = change.Hold,
        Spent = before.Spent.Plus(change.Spend ?? Spend.Zero),
        StateSpent = before.StateSpent.Plus(change.Spend ?? Spend.Zero),
        Deadline = null,
    };

    private static WorkflowInstance Cancelled(WorkflowInstance before, WorkflowChange change) =>
        before with { Ended = InstanceEnd.Cancelled, Hold = null, TakenOver = false, Deadline = null, Paused = null };

    private static WorkflowInstance Registered(WorkflowInstance before, WorkflowChange change)
    {
        var evidence = change.Evidence ?? throw new InvalidOperationException("An 'evidence' change carries the evidence");
        return before with { Evidence = [.. before.Evidence.Where(e => e.Kind != evidence.Kind || e.Sha != evidence.Sha), evidence] };
    }

    private static WorkflowInstance Unparked(WorkflowInstance before, WorkflowChange change)
    {
        var loops = Sorted(before.Loops);
        var findings = Sorted(before.Findings);
        if (before.Hold?.Loop is { } loop)
        {
            loops[loop] = 0;
            _ = findings.Remove(loop);
        }

        return before with
        {
            Hold = null,
            Loops = loops,
            Findings = findings,
            Extra = before.Extra.Plus(change.Extra ?? Spend.Zero),
            StateSpent = Spend.Zero,
            Deadline = change.Deadline,
            Paused = before.TakenOver ? change.Paused : before.Paused,
            Retries = 0,
        };
    }

    private static WorkflowInstance TakenOver(WorkflowInstance before, WorkflowChange change) => before with { TakenOver = true, Paused = change.Paused, Deadline = null };

    private static WorkflowInstance HandedBack(WorkflowInstance before, WorkflowChange change) => before with { TakenOver = false, Paused = null, Deadline = change.Deadline };

    private static WorkflowInstance Retried(WorkflowInstance before, WorkflowChange change) =>
        before with { Retries = before.Retries + 1, Deadline = change.Deadline, EnteredAt = change.At };

    private static WorkflowInstance Extended(WorkflowInstance before, WorkflowChange change) => before with { Deadline = change.Deadline };

    private static SortedDictionary<string, T> Sorted<T>(IReadOnlyDictionary<string, T> values) =>
        new(values.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal);
}
