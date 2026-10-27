#pragma warning disable MA0182 // registered and read by the build in the next commit
using Aw.Context.Core.Records;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The map build's input from the record store (spec 2026-10-27 §7.1, §7.5): doc and tracker records as 'record' nodes, cited paths and PR files as 'mentions' edges.</summary>
/// <param name="Records">The store.</param>
/// <param name="Symbols">The code index.</param>
internal sealed record MapRecordSources(IRecordStore Records, ICodeSymbolSource Symbols)
{
    /// <summary>Most resolved paths a pull request keeps as edges.</summary>
    internal const int TouchCap = 50;

    /// <summary>Reads every record in scope.</summary>
    /// <param name="input">What the build has.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The nodes, edges and report lines; the store's error when it can't be read.</returns>
    internal ValueTask<Result<MapRecordReading>> Read(MapRecordInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
}
