using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The separate-login credential source (sandbox spec SB18, SB-8): a stub until the red tests pass.</summary>
internal sealed class SeparateLoginCredentialSource : IModelCredentialSource
{
    private readonly string _sindriHome;
    private readonly TimeProvider _clock;

    internal SeparateLoginCredentialSource(string sindriHome, TimeProvider clock)
    {
        _sindriHome = sindriHome;
        _clock = clock;
    }

    /// <inheritdoc />
    public ModelLoginSource Id => throw new NotSupportedException();

    /// <inheritdoc />
    public bool Supports(AgentId target) => throw new NotSupportedException();

    /// <inheritdoc />
    public LoginStatus Probe(AgentId target) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<PreparedLogin>> Prepare(AgentId target, LaunchId launch, CancellationToken ct) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Release(LoginHandle handle) => throw new NotSupportedException(_sindriHome + _clock);
}
