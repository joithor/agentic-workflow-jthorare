using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>The throw-away request Check again plans and self-checks (sandbox spec §8): a fixture, never a real launch.</summary>
public interface ISandboxFixture
{
    /// <summary>Builds the fixture request.</summary>
    /// <returns>The request, or null when this box has no fixture to check (the self-check is then skipped, not failed).</returns>
    public SandboxRequest? Request();
}
