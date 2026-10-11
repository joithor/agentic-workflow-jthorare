using System.Globalization;

namespace Aw.Sindri.Host.Commands;

/// <summary>The arguments of a 'sindri workflow' verb: positionals, single options, repeatable options and flags. An unknown option is an error.</summary>
internal sealed class WorkflowArgs
{
    private static readonly HashSet<string> _flags = new(StringComparer.Ordinal) { "--json", "--all", "--empty-diff" };
    private static readonly HashSet<string> _repeatable = new(StringComparer.Ordinal) { "--set", "--finding" };
    private static readonly HashSet<string> _single = new(StringComparer.Ordinal)
    {
        "--attempt", "--base", "--expect-version", "--sha", "--tokens", "--turns", "--reason", "--detail",
    };

    private readonly Dictionary<string, List<string>> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);

    private WorkflowArgs()
    {
    }

    internal List<string> Positionals { get; } = [];

    internal bool Json => _set.Contains("--json");

    /// <summary>Parses <paramref name="args"/>, or returns the problem.</summary>
    internal static (WorkflowArgs? Args, string? Problem) Parse(IReadOnlyList<string> args)
    {
        var parsed = new WorkflowArgs();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (_flags.Contains(a))
            {
                _ = parsed._set.Add(a);
            }
            else if (_single.Contains(a) || _repeatable.Contains(a))
            {
                if (i + 1 >= args.Count || (!_repeatable.Contains(a) && parsed._options.ContainsKey(a)))
                {
                    return (null, i + 1 >= args.Count ? $"{a} needs a value" : $"{a} is given twice");
                }

                parsed.Add(a, args[++i]);
            }
            else if (a.StartsWith("--", StringComparison.Ordinal))
            {
                return (null, $"unknown option {a}");
            }
            else
            {
                parsed.Positionals.Add(a);
            }
        }

        return (parsed, null);
    }

    internal bool Flag(string name) => _set.Contains(name);

    internal string? Option(string name) => _options.TryGetValue(name, out var values) ? values[0] : null;

    internal IReadOnlyList<string> All(string name) => _options.TryGetValue(name, out var values) ? values : [];

    internal long? Number(string name) =>
        Option(name) is { } text && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

    /// <summary>What is wrong with the start options (--attempt from 1, --base a sha), or null.</summary>
    internal string? StartProblem() => Option("--red-stub");

    /// <summary>True when a numeric option is present but isn't a whole number.</summary>
    internal bool BadNumber(string name) => Option(name) is not null && Number(name) is null;

    private void Add(string name, string value)
    {
        if (!_options.TryGetValue(name, out var values))
        {
            _options[name] = values = [];
        }

        values.Add(value);
    }
}
