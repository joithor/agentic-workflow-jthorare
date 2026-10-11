using System.Security.Cryptography;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>A recipe's hash: SHA-256 over the AwJson of its <see cref="Recipe.Canonical"/> form.</summary>
public static class RecipeHashing
{
    /// <summary>The hash of <paramref name="canonicalJson"/>.</summary>
    /// <param name="canonicalJson">The canonical recipe JSON, UTF-8.</param>
    /// <returns>The hash.</returns>
    public static RecipeHash Of(ReadOnlySpan<byte> canonicalJson) => RecipeHash.From(Convert.ToHexStringLower(SHA256.HashData(canonicalJson)));
}
