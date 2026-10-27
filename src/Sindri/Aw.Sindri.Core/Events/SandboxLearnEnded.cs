using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'sandbox.learn-ended' payload (sandbox spec §3.6): the owner ended network learn mode early with 'sindri sandbox learn off'. A tightening, so it needs no approval.
/// Key 'sandbox.learn-ended:&lt;repo|all&gt;:&lt;at ticks&gt;'.
/// </summary>
/// <param name="Repo">The repo whose learn mode ended, or null for every repo.</param>
/// <param name="At">When the owner ended it.</param>
public sealed record SandboxLearnEnded([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RepoName? Repo, DateTimeOffset At);
