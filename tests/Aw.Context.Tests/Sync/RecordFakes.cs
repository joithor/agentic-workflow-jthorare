using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Sync;

/// <summary>The record store and an empty feed catalog for a test world that composes the context features, so the sync service resolves.</summary>
internal static class RecordFakes
{
    /// <summary>Adds a fake record store and a catalog with no feeds.</summary>
    /// <param name="services">The world's services.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddRecordFakes(this IServiceCollection services) =>
        services.AddSingleton<IRecordStore>(new FakeRecordStore()).AddSingleton<IRecordFeedCatalog>(new FakeRecordFeedCatalog());
}
