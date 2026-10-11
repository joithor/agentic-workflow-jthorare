using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Infrastructure.Recipes;

/// <summary>
/// The recipe catalog: the built-in recipes embedded in this build (approved) and the YAML files in
/// &lt;home&gt;/recipes (unapproved: they can't name capability blocks), each validated against the block registry and the
/// set of recipe ids found. Read on every call, so a changed file is a changed recipe (a new hash) at once.
/// </summary>
internal sealed class FileRecipeCatalog(SindriSettings settings, IBlockRegistry blocks, JsonSerializerOptions options) : IRecipeCatalog
{
    private const string BuiltinPrefix = "Aw.Sindri.Infrastructure.Recipes.Builtin.";

    public async ValueTask<Result<IReadOnlyList<CatalogEntry>>> List(CancellationToken cancellationToken) =>
        (await Load(cancellationToken).ConfigureAwait(false)).Select(r => r.Entry).ToList();

    public async ValueTask<Result<ValidatedRecipe>> Get(RecipeId id, CancellationToken cancellationToken)
    {
        var all = await Load(cancellationToken).ConfigureAwait(false);
        var found = all.FirstOrDefault(r => r.Entry.Id == id && r.Recipe is not null) ?? all.FirstOrDefault(r => r.Entry.Id == id);
        return found switch
        {
            { Recipe: { } recipe } => recipe,
            { } invalid => SindriErrors.RecipeUnknown.With($"recipe {id.Value} ({invalid.Entry.Source}) is invalid: {string.Join("; ", invalid.Entry.Problems)}"),
            _ => SindriErrors.RecipeUnknown.With($"no recipe {id.Value} is registered (fix: sindri workflow recipes)"),
        };
    }

    public async ValueTask<Result<CatalogEntry>> Inspect(string path, CancellationToken cancellationToken)
    {
        var text = await ReadFile(path, cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return text.Error;
        }

        var known = (await Load(cancellationToken).ConfigureAwait(false)).Select(r => r.Entry.Id).OfType<RecipeId>().ToHashSet();
        return Validate(path, text.Value, approved: false, known).Entry;
    }

    private async ValueTask<IReadOnlyList<LoadedRecipe>> Load(CancellationToken ct)
    {
        var sources = Builtins().ToList();
        foreach (var file in RecipeFiles())
        {
            var text = await ReadFile(file, ct).ConfigureAwait(false);
            sources.Add((file, text.IsError ? null : text.Value, Approved: false));
        }

        var known = new HashSet<RecipeId>();
        foreach (var (_, text, _) in sources.Where(s => s.Text is not null))
        {
            if (RecipeReading.Read(text!, options).Recipe is { } recipe)   // why: filtered on Text above
            {
                _ = known.Add(recipe.Id);
            }
        }

        var loaded = new List<LoadedRecipe>();
        foreach (var (source, text, approved) in sources)
        {
            var entry = text is null ? Unreadable(source) : Validate(source, text, approved, known);
            loaded.Add(Distinct(entry, loaded));
        }

        return loaded;
    }

    private LoadedRecipe Validate(string source, string text, bool approved, IReadOnlySet<RecipeId> known)
    {
        var read = RecipeReading.Read(text, options);
        if (read is not { Recipe: { } recipe, Hash: { } hash })
        {
            return new LoadedRecipe(new CatalogEntry(source, Id: null, Version: null, Hash: null, approved, [read.Problem!]), Recipe: null);   // why: a read without a recipe carries its problem
        }

        var problems = RecipeValidator.Validate(recipe, hash, approved, blocks, known);
        var entry = new CatalogEntry(source, recipe.Id, recipe.Version, hash, approved, problems);
        return new LoadedRecipe(entry, problems.Count == 0 ? new ValidatedRecipe(recipe, hash, approved, source) : null);
    }

    // A second recipe with an id already registered (a file shadowing a built-in, or two files) is refused.
    private static LoadedRecipe Distinct(LoadedRecipe candidate, List<LoadedRecipe> earlier)
    {
        var first = earlier.FirstOrDefault(e => e.Entry.Id is { } id && id == candidate.Entry.Id);
        if (first is null)
        {
            return candidate;
        }

        var problem = new RecipeProblem(RecipeChecks.Shape, "id", $"recipe {candidate.Entry.Id?.Value} is already registered by {first.Entry.Source}");
        return new LoadedRecipe(candidate.Entry with { Problems = [.. candidate.Entry.Problems, problem] }, Recipe: null);
    }

    private static LoadedRecipe Unreadable(string source) =>
        new(new CatalogEntry(source, Id: null, Version: null, Hash: null, Approved: false, [new RecipeProblem(RecipeChecks.Schema, "$", "the file can't be read")]), Recipe: null);

    private static IEnumerable<(string Source, string? Text, bool Approved)> Builtins()
    {
        var assembly = typeof(FileRecipeCatalog).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(r => r.StartsWith(BuiltinPrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"Resource '{resource}' is listed but can't be opened.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            yield return ($"builtin:{resource[BuiltinPrefix.Length..]}", reader.ReadToEnd(), true);
        }
    }

    private IEnumerable<string> RecipeFiles() =>
        Directory.Exists(settings.RecipesDirectory)
            ? Directory.EnumerateFiles(settings.RecipesDirectory, "*.yaml").Order(StringComparer.Ordinal)
            : [];

    private static async ValueTask<Result<string>> ReadFile(string path, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.RecipeUnknown.With($"{path} can't be read: {e.Message}");
        }
    }
}
