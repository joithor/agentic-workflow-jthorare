using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Testing.GitHub;

namespace Aw.Connectors.Tests.GitHub;

public sealed class FakeGitHubReaderContractTests : GitHubReaderContract
{
    protected override IGitHubReader Reader(IReadOnlyList<GitHubIssue> issues)
    {
        var fake = new FakeGitHubReader();
        foreach (var issue in issues)
        {
            _ = fake.Add(Repo, issue);
        }

        return fake;
    }
}
