using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'workflow.pr:' block (PR babysitter spec W27): the pull request keys. 'autofixCap' bounds the CI fixes one pull request gets, across
/// every babysitter instance that follows it ('gate.pr.autofix-cap').
/// </summary>
/// <param name="AutofixCap">The most CI fixes pushed to one pull request, from 1 to <see cref="MaxAutofixCap"/> (default <see cref="DefaultAutofixCap"/>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AutofixCap = null)
{
    /// <summary>The cap when the profile gives none.</summary>
    public const int DefaultAutofixCap = 3;

    /// <summary>The highest cap a profile may set.</summary>
    public const int MaxAutofixCap = 5;

    /// <summary>The cap <paramref name="profile"/> sets, or the default.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The number of CI fixes one pull request gets.</returns>
    public static int AutofixCapOf(SindriProfile profile) => throw new NotSupportedException();
}
