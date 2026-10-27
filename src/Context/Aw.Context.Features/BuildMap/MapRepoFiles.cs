using Aw.Context.Core.Engine;

namespace Aw.Context.Features.BuildMap;

/// <summary>One repo as the build read it: its paths at HEAD and each path's git blob id.</summary>
/// <param name="Name">The repo name.</param>
/// <param name="Paths">Every path of the repo at the commit, '/'-separated.</param>
/// <param name="Blobs">The blob id of a path at HEAD; a doc's stored version is compared with it.</param>
internal sealed record MapRepoFiles(ContextRepoName Name, IReadOnlyList<string> Paths, IReadOnlyDictionary<string, string> Blobs);
