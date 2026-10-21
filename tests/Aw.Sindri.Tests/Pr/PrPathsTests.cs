using Aw.Kernel.Testing;
using Aw.Sindri.Features.Pr;

namespace Aw.Sindri.Tests.Pr;

/// <summary>What a path in a pull request is by convention: a test, CI configuration, or source.</summary>
[Lane(TestLane.Unit)]
public sealed class PrPathsTests
{
    [Theory]
    [InlineData("tests/Aw.Foo.Tests/BarTests.cs")]
    [InlineData("test/foo.js")]
    [InlineData("src/app/__tests__/foo.ts")]
    [InlineData("src/foo.test.ts")]
    [InlineData("src/foo.spec.tsx")]
    [InlineData("pkg/foo_test.go")]
    [InlineData("pkg/test_foo.py")]
    [InlineData("spec/models/user_spec.rb")]
    [InlineData("src/Aw.Foo.Tests/Bar.cs")]
    [InlineData("src/FooTests.cs")]
    public void A_test_path_is_a_test(string path) => Assert.True(PrPaths.IsTest(path));

    [Theory]
    [InlineData("src/Foo.cs")]
    [InlineData("src/Latest.cs")]
    [InlineData("src/contest/entry.ts")]
    [InlineData("README.md")]
    public void Anything_else_is_not(string path) => Assert.False(PrPaths.IsTest(path));

    [Theory]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData(".github/actions/setup/action.yml")]
    [InlineData(".gitlab-ci.yml")]
    [InlineData(".circleci/config.yml")]
    [InlineData("azure-pipelines.yml")]
    [InlineData(".buildkite/pipeline.yml")]
    [InlineData(".travis.yml")]
    [InlineData("Jenkinsfile")]
    [InlineData("sub/.github/workflows/ci.yml")]
    public void A_CI_config_path_is_CI_config(string path) => Assert.True(PrPaths.IsCiConfig(path));

    [Theory]
    [InlineData("src/Foo.cs")]
    [InlineData(".github/CODEOWNERS")]
    [InlineData("docs/ci.md")]
    public void Anything_else_is_not_CI_config(string path) => Assert.False(PrPaths.IsCiConfig(path));

    [Theory]
    [InlineData("src/Foo.cs", true)]
    [InlineData("lib/util.ts", true)]
    [InlineData("README.md", false)]
    [InlineData("docs/usage.md", false)]
    [InlineData("tests/FooTests.cs", false)]
    [InlineData(".github/workflows/ci.yml", false)]
    public void Source_is_neither_a_test_nor_CI_config_nor_a_document(string path, bool source) => Assert.Equal(source, PrPaths.IsSource(path));
}
