using Aw.Kernel.Data;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

public sealed class LedgerFileTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Each_migration_applies_on_the_previous_schema_and_the_outbox_table_stays_third()
    {
        var path = Path.Combine(_home.Path, "steps.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);

        for (var version = 1; version <= LedgerDatabase.Migrations.Count; version++)
        {
            var step = await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken, targetVersion: version);
            Assert.True(step.IsSuccess, step.IsError ? step.Error.Message : null);
        }

        Assert.Equal(["events", "lease", "outbox_commands", "workflow"], LedgerDatabase.Migrations.Select(m => m.Name), StringComparer.Ordinal);
        Assert.Equal(["events", "lease", "outbox_commands", "workflow_instances"], Tables(path));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_workflow_migration_keeps_existing_events_on_a_copy_of_the_s1_schema()
    {
        var path = Path.Combine(_home.Path, "s1.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);
        _ = (await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken, targetVersion: 3)).Value;
        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json) "
                + "VALUES ('e1', 'process.started', 1, '2026-10-11T00:00:00Z', 'host:sindri', 'local', 0, 'c1', 1, 'k1', '{}')";
            _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var upgraded = await runner.Migrate(LedgerDatabase.Migrations, TestContext.Current.CancellationToken);

        Assert.Equal([4], upgraded.Value.Applied);
        await using var check = new SqliteConnection($"Data Source={path}");
        await check.OpenAsync(TestContext.Current.CancellationToken);
        await using var read = check.CreateCommand();
        read.CommandText = "SELECT idempotency_key, instance IS NULL FROM events";
        await using var reader = await read.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(("k1", 1L), (reader.GetString(0), reader.GetInt64(1)));
    }

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_first_write_creates_an_owner_only_file_and_reads_never_create_one()
    {
        var home = Path.Combine(_home.Path, "home");
        var probe = new FakeProcessProbe();
        var stores = SqliteStores.Create(home, new FakeTimeProvider(), probe);

        Assert.Null((await stores.Lease.Read(TestContext.Current.CancellationToken)).Value);
        Assert.Equal(0, (await stores.Ledger.Schema(TestContext.Current.CancellationToken)).Value.Applied);
        Assert.False(File.Exists(Path.Combine(home, "ledger.db")));

        _ = (await stores.Lease.Acquire(probe.Self(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(home));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(home, "ledger.db")));
        Assert.True((await stores.Ledger.Schema(TestContext.Current.CancellationToken)).Value.IsCurrent);
    }

    private static List<string> Tables(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT IN ('schema_version', 'sqlite_sequence') ORDER BY name";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
