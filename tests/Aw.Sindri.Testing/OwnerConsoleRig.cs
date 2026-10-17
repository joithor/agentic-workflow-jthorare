using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IOwnerConsole"/> under test, a way to feed it answers and what its terminal showed.</summary>
public abstract class OwnerConsoleRig
{
    /// <summary>Gets the console under test.</summary>
    public abstract IOwnerConsole Console { get; }

    /// <summary>Gets everything the terminal showed (prompts, echoed lines).</summary>
    public abstract string Terminal { get; }

    /// <summary>Queues what the person types next: each answer is typed and then Enter is pressed; null is end of input.</summary>
    /// <param name="answers">The answers.</param>
    public abstract void Feed(params string?[] answers);
}
