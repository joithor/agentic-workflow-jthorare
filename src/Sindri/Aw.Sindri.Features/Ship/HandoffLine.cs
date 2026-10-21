using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Ship;

/// <summary>Where the phase sits in its line, from <c language="text">fact:lineContext</c> (line spec §7.6): the branch the phase built on, the branch the PR opens against and the packets before the phase's.</summary>
/// <param name="Line">The line's name.</param>
/// <param name="Phase">The phase's id.</param>
/// <param name="StackParent">The branch of the phase this one is stacked on; null for a phase on the default branch.</param>
/// <param name="BaseBranch">The branch a phase on the default branch opens against.</param>
/// <param name="Earlier">The hashes of the scope, design and plan packets the line context names, in that order.</param>
internal sealed record HandoffLine(string Line, string Phase, string? StackParent, string BaseBranch, IReadOnlyList<string> Earlier)
{
    /// <summary>The branch the default-branch PRs open against when the line context names none.</summary>
    internal const string DefaultBase = "main";

    private static readonly string[] _earlierKeys = ["scope", "design", "plan"];

    /// <summary>The phase's branch, as step.phase.seed names it.</summary>
    internal string Branch => $"sindri/{Line}/{Phase}";

    /// <summary>The branch the PR opens against.</summary>
    internal string Base => StackParent ?? BaseBranch;

    /// <summary>Reads the line context; null when it names no line or no phase.</summary>
    /// <param name="context">The fact's JSON.</param>
    /// <returns>The line.</returns>
    internal static HandoffLine? Of(JsonNode? context)
    {
        if (ShipPacket.Text(context, "line") is not { } line || ShipPacket.Text(context, "phase") is not { } phase)
        {
            return null;
        }

        var packets = context?["packets"];
        string[] earlier = [.. _earlierKeys.Select(k => ShipPacket.Text(packets, k)).OfType<string>()];
        return new HandoffLine(line, phase, ShipPacket.Text(context, "stackParent"), ShipPacket.Text(context, "baseBranch") ?? DefaultBase, earlier);
    }
}
