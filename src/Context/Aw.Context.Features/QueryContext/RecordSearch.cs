using Aw.Context.Core.Records;
using Aw.Context.Core.Search;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the query reads the record store with: its lexical leg and the store itself (spec §6.1).</summary>
/// <param name="Leg">The lexical leg.</param>
/// <param name="Store">The record store the leg's hits are read back from.</param>
internal sealed record RecordSearch(ILexicalLeg Leg, IRecordStore Store);
