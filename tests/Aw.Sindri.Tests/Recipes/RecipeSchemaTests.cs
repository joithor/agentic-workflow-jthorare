using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Tests.Recipes;

/// <summary>
/// The recipe JSON Schema (factory addendum §17.28.1: recipes are validated by a JSON Schema in schemas/) is generated
/// from the <see cref="Recipe"/> record and committed as schemas/sindri/recipe.schema.json. A record change that isn't
/// regenerated fails here; regenerate with AW_GOLDEN_UPDATE=1 (refused in CI) and review the diff.
/// </summary>
public sealed class RecipeSchemaTests
{
    [Fact, Trait("Lane", "integration")]   // reads (and in update mode writes) schemas/sindri/
    public void The_committed_schema_is_generated_from_the_record()
    {
        var actual = Render(Generate());
        var path = Path.GetFullPath(Path.Combine(Golden.SourcePath(typeof(RecipeSchemaTests).Assembly, "Recipes"), "..", "..", "..", "schemas", "sindri", "recipe.schema.json"));

        if (string.Equals(Environment.GetEnvironmentVariable(Golden.UpdateVariable), "1", StringComparison.Ordinal)
            && !string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.Ordinal))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            Assert.Fail($"{path} regenerated; review the diff, then run again without {Golden.UpdateVariable}.");
        }

        Assert.True(File.Exists(path), $"{path} is missing; generate it with {Golden.UpdateVariable}=1.");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static JsonObject Generate()
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(SliceHarness.Json, typeof(Recipe), new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (context, node) => Transform(context.TypeInfo.Type, node),
        });
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = "https://github.com/jthorare/agentic-workflow/schemas/sindri/recipe.schema.json",
            ["title"] = "Sindri workflow recipe (docs/csharp/sindri-engine.md)",
        };
        foreach (var (key, value) in schema.AsObject().ToList())
        {
            root[key] = value?.DeepClone();
        }

        return root;
    }

    // A typed id has a user-code converter, so the exporter can't see its shape: describe it.
    private static JsonNode Transform(Type type, JsonNode node)
    {
        var id = Nullable.GetUnderlyingType(type) ?? type;
        if (id.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<,>)))
        {
            return new JsonObject
            {
                ["type"] = id == type ? "string" : new JsonArray("string", "null"),
                ["pattern"] = id == typeof(RecipeHash) ? "^[0-9a-f]{64}$" : "^[a-z][a-z0-9-]*(\\.[a-z0-9][a-z0-9-]*)+$",
            };
        }

        if (node is JsonObject array && type.IsGenericType && type.GetGenericArguments() is [var item] && item == typeof(BlockId))
        {
            array["items"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[a-z][a-z0-9-]*(\\.[a-z0-9][a-z0-9-]*)+$" };
        }

        return node;
    }

    private static string Render(JsonNode node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, IndentSize = 2, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }
}
