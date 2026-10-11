using System.Globalization;

namespace Aw.Sindri.Core.Recipes;

/// <summary>A recipe duration: a whole number and s, m, h or d ('30s', '10m', '4h', '2d'), from 1 second to 30 days.</summary>
public static class RecipeDuration
{
    /// <summary>The longest duration.</summary>
    public static readonly TimeSpan Max = TimeSpan.FromDays(30);

    /// <summary>Parses <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The duration, or null when it isn't one.</returns>
    public static TimeSpan? Parse(string? text)
    {
        if (text is null || text.Length < 2 || !long.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return null;
        }

        var bounded = Math.Min(n, 10_000_000);
        TimeSpan? value = text[^1] switch
        {
            's' => TimeSpan.FromSeconds(bounded),
            'm' => TimeSpan.FromMinutes(bounded),
            'h' => TimeSpan.FromHours(bounded),
            'd' => TimeSpan.FromDays(Math.Min(bounded, 100_000)),
            _ => null,
        };
        return value is { } v && v >= TimeSpan.FromSeconds(1) && v <= Max ? v : null;
    }
}
