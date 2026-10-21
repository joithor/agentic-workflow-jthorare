using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Testing.Forge;

/// <summary>
/// An <see cref="IPullRequests"/> in memory: pull requests a test adds, check logs it sets, and every push and rerun recorded. A push whose lease is the pull request's
/// head moves the head (its checks stay on the old commit until the test sets new ones); any other lease is a <see cref="PullPushOutcome.HeadMoved"/>.
/// Held to <see cref="PullRequestsContract"/>.
/// </summary>
public sealed class FakePullRequests : IPullRequests
{
    private readonly Dictionary<string, PullView> _pulls = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Link, string Check), string> _logs = [];
    private readonly List<PullPush> _pushes = [];
    private readonly List<(string Link, string Sha)> _reruns = [];

    /// <summary>Makes the pull request exist.</summary>
    /// <param name="pull">The pull request.</param>
    public void Add(PullView pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        _pulls[pull.Link] = pull;
    }

    /// <summary>Changes a pull request.</summary>
    /// <param name="link">Its link.</param>
    /// <param name="change">The change.</param>
    public void Change(string link, Func<PullView, PullView> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _pulls[link] = change(_pulls[link]);
    }

    /// <summary>Sets a check's log.</summary>
    /// <param name="link">The pull request.</param>
    /// <param name="check">The check.</param>
    /// <param name="log">Its log.</param>
    public void SetLog(string link, string check, string log) => _logs[(link, check)] = log;

    /// <summary>What the next pushes answer instead of following the lease: null follows it.</summary>
    public PullPushOutcome? PushAnswer { get; set; }

    /// <summary>Called with each accepted push, so a test can move a real remote too.</summary>
    public Func<PullPush, CancellationToken, ValueTask>? OnPush { get; set; }

    /// <summary>How many failed checks a rerun is asked to run again; null counts the failing checks of the pull request.</summary>
    public int? RerunAnswer { get; set; }

    /// <summary>Every push asked for, in order.</summary>
    public IReadOnlyList<PullPush> Pushes => _pushes;

    /// <summary>Every rerun asked for, in order.</summary>
    public IReadOnlyList<(string Link, string Sha)> Reruns => _reruns;

    /// <summary>How many times <see cref="Get"/> was called.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult<Result<PullView>>(_pulls.TryGetValue(link, out var found) ? found : NotFound(link));
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(_logs.TryGetValue((link, check), out var log) ? log : NotFound($"{link} check {check}"));

    /// <inheritdoc/>
    public async ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);
        _pushes.Add(push);
        if (!_pulls.TryGetValue(push.Link, out var pull))
        {
            return NotFound(push.Link);
        }

        var outcome = PushAnswer ?? (string.Equals(pull.HeadSha, push.LeaseSha, StringComparison.Ordinal) ? PullPushOutcome.Pushed : PullPushOutcome.HeadMoved);
        if (outcome == PullPushOutcome.Pushed)
        {
            _pulls[push.Link] = pull with { HeadSha = push.Sha };
            if (OnPush is { } hook)
            {
                await hook(push, cancellationToken).ConfigureAwait(false);
            }
        }

        return outcome;
    }

    /// <inheritdoc/>
    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken)
    {
        _reruns.Add((link, sha));
        return ValueTask.FromResult<Result<int>>(_pulls.TryGetValue(link, out var pull) ? RerunAnswer ?? pull.Checks.Failing.Count : NotFound(link));
    }

    private static Error NotFound(string what) => WorkflowErrors.EffectFailed.With($"no pull request or log for {what}");
}
