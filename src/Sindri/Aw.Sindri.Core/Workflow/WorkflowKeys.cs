using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The idempotency keys of workflow events (factory addendum §17.20, §17.28.1): the instance (whose id is derived from
/// tenant, recipe, item and attempt, or for a child from its parent and the parent's version), the transition named with
/// the version it applies to, and the sha, so a retried or replayed event is a no-op and a later loop round is a new key.
/// Also the instance ids and the tree's correlation id, all derived, so replay is stable.
/// </summary>
public static class WorkflowKeys
{
    /// <summary>The instance id for (tenant, recipe, item, attempt).</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="item">The item.</param>
    /// <param name="attempt">The attempt.</param>
    /// <returns>The id.</returns>
    public static InstanceId Instance(string tenant, RecipeId recipe, ItemId item, int attempt)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{tenant}\n{recipe.Value}\n{item.Value}\n{attempt}")));
        return InstanceId.From("wf-" + Convert.ToHexStringLower(digest)[..12]);
    }

    /// <summary>
    /// The id of the child workflow <paramref name="parent"/> starts at <paramref name="parentVersion"/>: each run of a
    /// child wait state, in every parent attempt, gets its own child.
    /// </summary>
    /// <param name="parent">The parent instance.</param>
    /// <param name="recipe">The child's recipe.</param>
    /// <param name="parentVersion">The parent's version when it entered the child wait state.</param>
    /// <returns>The id.</returns>
    public static InstanceId Child(InstanceId parent, RecipeId recipe, long parentVersion)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"child\n{parent.Value}\n{recipe.Value}\n{parentVersion}")));
        return InstanceId.From("wf-" + Convert.ToHexStringLower(digest)[..12]);
    }

    /// <summary>The key of an instance's start.</summary>
    /// <param name="instance">The new instance's id.</param>
    /// <returns>The key: 'wf:&lt;instance&gt;:start'.</returns>
    public static string Start(InstanceId instance) => Prefix(instance) + ":start";

    /// <summary>The key of a recipe transition from <paramref name="instance"/>'s current version.</summary>
    /// <param name="instance">The instance before the transition.</param>
    /// <param name="to">The state it enters.</param>
    /// <param name="on">The signal that asked for it.</param>
    /// <param name="sha">The sha it is bound to, if any.</param>
    /// <returns>The key: 'wf:&lt;instance&gt;:&lt;from&gt;&gt;&lt;to&gt;:&lt;on&gt;@v&lt;version&gt;:&lt;sha or -&gt;'.</returns>
    public static string Transition(WorkflowInstance instance, string to, string on, HeadSha? sha)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return $"{Prefix(instance.Id)}:{instance.State}>{to}{SignalSuffix(on, instance.Version, sha)}";
    }

    /// <summary>
    /// True when <paramref name="key"/> is the key a signal recorded at that version: it ends with
    /// <paramref name="suffix"/> and names a transition ('from&gt;to') just before it, so a timer or command key that
    /// happens to end the same way doesn't count.
    /// </summary>
    /// <param name="key">An event's idempotency key.</param>
    /// <param name="suffix">The signal's <see cref="SignalSuffix"/>.</param>
    /// <returns>True for the signal's key.</returns>
    public static bool IsSignalKey(string key, string suffix)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(suffix);
        if (!key.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var head = key.AsSpan(0, key.Length - suffix.Length);
        return head[(head.LastIndexOf(':') + 1)..].Contains('>');
    }

    /// <summary>The end of every key a signal at <paramref name="version"/> records, whichever transition it took.</summary>
    /// <param name="on">The signal.</param>
    /// <param name="version">The version it applied to.</param>
    /// <param name="sha">Its sha.</param>
    /// <returns>The suffix.</returns>
    public static string SignalSuffix(string on, long version, HeadSha? sha) =>
        string.Create(CultureInfo.InvariantCulture, $":{on}@v{version}:{sha?.Value ?? "-"}");

    /// <summary>The key of any other change (park, timeout, takeover, evidence…) from <paramref name="instance"/>'s current version.</summary>
    /// <param name="instance">The instance before the change.</param>
    /// <param name="what">What changed, for example 'parked' or 'timeout:review'.</param>
    /// <returns>The key.</returns>
    public static string Change(WorkflowInstance instance, string what)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return string.Create(CultureInfo.InvariantCulture, $"{Prefix(instance.Id)}:{what}@v{instance.Version}:-");
    }

    /// <summary>The correlation id every event of a workflow tree shares.</summary>
    /// <param name="instance">The tree's root instance id.</param>
    /// <returns>The correlation id.</returns>
    internal static Guid Correlation(InstanceId instance) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("correlation\n" + instance.Value)).AsSpan(0, 16));

    private static string Prefix(InstanceId instance) => "wf:" + instance.Value;
}
