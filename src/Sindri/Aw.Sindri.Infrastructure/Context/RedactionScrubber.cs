using Aw.Redaction;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Infrastructure.Context;

/// <summary>The egress scrubber over Aw.Redaction's built-in rules: one scrubber for the whole build.</summary>
internal sealed class RedactionScrubber(IRedactor redactor) : ITextScrubber
{
    public ScrubbedText Scrub(string text)
    {
        var result = redactor.Scrub(text);
        return new ScrubbedText(result.Text, result.Outcome != ScrubOutcome.Clean);
    }
}
