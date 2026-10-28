using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri index sync [source] | status [--json] | purge &lt;source&gt;' (owned-index spec §5.5, §5.8, §10): clients of the control plane.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job a sync starts.</param>
/// <param name="terminal">Whether standard input and output are a terminal (purge asks its typed yes only there).</param>
/// <param name="input">Where the typed yes is read.</param>
/// <param name="ensureRunning">Starts the control plane for sync (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class IndexVerbs(ControlPlaneClient client, Output output, JobFollower follower, bool terminal, TextReader input, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal const string Usage = "index sync [<source>] [--detach] [--json] [--no-start] | index status [--json] | index purge <source>";

    internal Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        _ = (client, output, follower, terminal, input, ensureRunning, args, interrupt, again);
        throw new NotSupportedException("red");
    }
}
