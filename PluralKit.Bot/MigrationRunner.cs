using System.Reflection;

using Npgsql;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class MigrationRunner
{
    public static async Task Run(CoreConfig config)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(config.Database);
        if (config.DatabasePassword != null)
            connectionString.Password = config.DatabasePassword;

        await using var dataSource = NpgsqlDataSource.Create(connectionString.ConnectionString);
        await using var connection = await dataSource.OpenConnectionAsync();

        var currentVersion = await CurrentVersion(connection);

        await ExecuteResource(connection, "clean.sql");

        var assembly = typeof(MigrationRunner).Assembly;
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.migrations.") && name.EndsWith(".sql"))
            .Select(name => new
            {
                Name = name,
                Version = int.Parse(name.Split('.')[^2])
            })
            .Where(migration => migration.Version > currentVersion)
            .OrderBy(migration => migration.Version);

        foreach (var migration in migrations)
            await ExecuteResource(connection, migration.Name);

        await ExecuteResource(connection, "views.sql");
        await ExecuteResource(connection, "functions.sql");

        if (Environment.GetEnvironmentVariable("SEED") == "true")
            await ExecuteResource(connection, "seed.sql");
    }

    private static async Task<int> CurrentVersion(NpgsqlConnection connection)
    {
        try
        {
            await using var command = new NpgsqlCommand("select schema_version from info", connection);
            var result = await command.ExecuteScalarAsync();
            return result is int version ? version : -1;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return -1;
        }
    }

    private static async Task ExecuteResource(NpgsqlConnection connection, string resourceSuffix)
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(resourceSuffix, StringComparison.Ordinal));

        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var sql = (await reader.ReadToEndAsync()).TrimStart('\uFEFF');

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}