using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A workflow instance's id: 'wf-' and 12 hex digits of SHA-256 over its tenant, recipe, item and attempt, so a retried start or a replay gives the same id.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<InstanceId>))]
public readonly partial struct InstanceId : ITypedId<InstanceId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An instance id is 'wf-' and 12 lower-case hex digits");

    [GeneratedRegex("^wf-[0-9a-f]{12}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
