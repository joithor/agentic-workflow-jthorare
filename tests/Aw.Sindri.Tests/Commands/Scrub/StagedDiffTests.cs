using Aw.Sindri.Host.Commands.Scrub;

namespace Aw.Sindri.Tests.Commands.Scrub;

/// <summary>Reading the added lines out of 'git diff --cached --unified=0'.</summary>
public sealed class StagedDiffTests
{
    [Fact, Trait("Lane", "unit")]
    public void Added_lines_carry_their_line_number_in_the_new_file()
    {
        const string diff = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -2,0 +3,2 @@\n+first\n+second\n@@ -9 +11 @@\n-old\n+new\n";

        var file = Assert.Single(StagedDiff.Parse(diff));

        Assert.Equal("a.txt", file.File);
        Assert.Equal([new AddedLine(3, "first"), new AddedLine(4, "second"), new AddedLine(11, "new")], file.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_added_line_that_looks_like_a_file_header_is_still_an_addition()
    {
        const string diff = "--- a/a.txt\n+++ b/a.txt\n@@ -0,0 +1,2 @@\n+++ b/evil.txt\n+KEY\n";

        var file = Assert.Single(StagedDiff.Parse(diff));

        Assert.Equal(["++ b/evil.txt", "KEY"], file.Lines.Select(static l => l.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_deleted_file_and_a_removal_only_change_add_nothing()
    {
        const string diff = "--- a/gone.txt\n+++ /dev/null\n@@ -1 +0,0 @@\n-bye\n--- a/b.txt\n+++ b/b.txt\n@@ -4 +3,0 @@\n-removed\n";

        Assert.Empty(StagedDiff.Parse(diff));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_with_a_space_loses_the_tab_git_adds_after_it()
    {
        const string diff = "--- a/a b.txt\t\n+++ b/a b.txt\t\n@@ -0,0 +1 @@\n+x\n";

        Assert.Equal("a b.txt", Assert.Single(StagedDiff.Parse(diff)).File);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_no_newline_marker_is_not_a_line()
    {
        const string diff = "--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-old\n\\ No newline at end of file\n+new\n\\ No newline at end of file\n";

        Assert.Equal(["new"], Assert.Single(StagedDiff.Parse(diff)).Lines.Select(static l => l.Text));
    }
}
