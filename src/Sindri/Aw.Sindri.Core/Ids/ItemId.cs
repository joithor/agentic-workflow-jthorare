using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>The work item a workflow instance runs for, for example a tracker key 'ITEM-12': 1 to 128 characters of letters, digits and . _ : / # -.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ItemId>))]
public readonly partial struct ItemId : ITypedId<ItemId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An item is 1 to 128 characters: letters, digits and . _ : / # - (starting with a letter or digit)");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:/#-]{0,127}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
