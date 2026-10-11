namespace Aw.Sindri.Core.Recipes;

/// <summary>The names of the recipe checks (factory addendum §17.28.5), as problems report them.</summary>
public static class RecipeChecks
{
    /// <summary>The recipe matches the schema (the JSON Schema in schemas/sindri/ is generated from the record).</summary>
    public const string Schema = "schema";

    /// <summary>States, transitions, loops and the initial state refer to each other consistently.</summary>
    public const string Shape = "shape";

    /// <summary>Every block id is registered, with the kind its place needs.</summary>
    public const string Block = "block";

    /// <summary>Every block's inputs are produced upstream, with the same type.</summary>
    public const string Input = "input";

    /// <summary>Capability-bearing blocks appear only in approved recipes.</summary>
    public const string Capability = "capability";

    /// <summary>Every state is reachable from the initial state.</summary>
    public const string Reachable = "reachable";

    /// <summary>Every non-terminal state has a way out.</summary>
    public const string Exit = "exit";

    /// <summary>Every wait and active state has a timeout.</summary>
    public const string Timeout = "timeout";

    /// <summary>Every loop has a cap.</summary>
    public const string Loop = "loop";

    /// <summary>Effects only use allowlisted command kinds.</summary>
    public const string Effect = "effect";

    /// <summary>The recipe hash matches its content.</summary>
    public const string Hash = "hash";
}
