using Npgsql;

namespace Rmg.IntegrationTests;

// Fresh throwaway database on the live PostgreSQL server, dropped on disposal.
public sealed record ScratchDb(string ConnectionString, string AdminConnectionString) : IAsyncDisposable
{
    public static async Task<ScratchDb> CreateAsync(string adminConnectionString)
    {
        var name = $"rmg_test_{Guid.NewGuid().ToString("N")[..8]}";

        await using var conn = new NpgsqlConnection(adminConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
        await cmd.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name };
        return new ScratchDb(builder.ConnectionString, adminConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        // Drop pooled connections first or the DROP fails with "database is being accessed by other users".
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private string DatabaseName => new NpgsqlConnectionStringBuilder(ConnectionString).Database!;
}
