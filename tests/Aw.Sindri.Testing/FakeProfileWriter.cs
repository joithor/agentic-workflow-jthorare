using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Testing;

/// <summary>An IProfileWriter over a profile held as text in memory, edited by the same `ProfileEdit` the file writer uses. Held to <see cref="ProfileWriterContract"/>.</summary>
public sealed class FakeProfileWriter : IProfileWriter
{
    /// <summary>Gets or sets the profile's text, or null when there is no profile file.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the user a profile created from nothing names.</summary>
    public string User { get; set; } = "thor";

    /// <summary>Gets or sets an error every write returns instead (the file couldn't be read).</summary>
    public Error? Error { get; set; }

    /// <summary>Gets the writes asked for, as `port token version`.</summary>
    public IList<string> Writes { get; } = [];

    /// <inheritdoc />
    public Result<ProfileWriteOutcome> SetOnyxSource(int port, SecretPointer token, string version)
    {
        ArgumentNullException.ThrowIfNull(token);
        Writes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{port} {token} {version}"));
        if (Error is { } error)
        {
            return error;
        }

        var edited = ProfileEdit.SetOnyxSource(Text, User, port, token.ToString(), version);
        if (edited.IsError)
        {
            return edited.Error;
        }

        var outcome = Text is null ? ProfileWriteOutcome.Created : string.Equals(Text, edited.Value, StringComparison.Ordinal) ? ProfileWriteOutcome.Unchanged : ProfileWriteOutcome.Changed;
        Text = edited.Value;
        return outcome;
    }
}
