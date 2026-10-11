using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The idempotency keys of workflow events (factory addendum §17.20, §17.28.1): (recipe, item, attempt, transition, sha),
/// where the transition is named with the version it applies to, so a retried or replayed event is a no-op and a later
/// loop round is a new key. Also the instance id and the instance's correlation id, both derived, so replay is stable.
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

    /// <summary>The key of a start.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="item">The item.</param>
    /// <param name="attempt">The attempt.</param>
    /// <returns>The key.</returns>
    public static string Start(RecipeId recipe, ItemId item, int attempt) => Prefix(recipe, item, attempt) + ":start";

    /// <summary>The key of a recipe transition from <paramref name="instance"/>'s current version.</summary>
    /// <param name="instance">The instance before the transition.</param>
    /// <param name="to">The state it enters.</param>
    /// <param name="on">The signal that asked for it.</param>
    /// <param name="sha">The sha it is bound to, if any.</param>
    /// <returns>The key: 'wf:&lt;recipe&gt;:&lt;item&gt;:&lt;attempt&gt;:&lt;from&gt;&gt;&lt;to&gt;:&lt;on&gt;@v&lt;version&gt;:&lt;sha or -&gt;'.</returns>
    public static string Transition(WorkflowInstance instance, string to, string on, HeadSha? sha)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return $"{Prefix(instance.Recipe, instance.Item, instance.Attempt)}:{instance.State}>{to}{SignalSuffix(on, instance.Version, sha)}";
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
        return string.Create(CultureInfo.InvariantCulture, $"{Prefix(instance.Recipe, instance.Item, instance.Attempt)}:{what}@v{instance.Version}:-");
    }

    /// <summary>The correlation id every event of an instance shares.</summary>
    /// <param name="instance">The instance id.</param>
    /// <returns>The correlation id.</returns>
    internal static Guid Correlation(InstanceId instance) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("correlation\n" + instance.Value)).AsSpan(0, 16));

    private static string Prefix(RecipeId recipe, ItemId item, int attempt) =>
        string.Create(CultureInfo.InvariantCulture, $"wf:{recipe.Value}:{item.Value}:{attempt}");
}
