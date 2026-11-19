using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Scada.Alarms.IntegrationTests;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace Scada.Alarms.IntegrationTests;

/// <summary>
/// Starts one disposable SQL Server container for the whole test assembly,
/// creates a simplified dbo.Alarms (only the columns used) and seeds known scenarios.
/// </summary>
public sealed partial class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "AlarmHistorian";

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using (var master = new SqlConnection(_container.GetConnectionString()))
            await master.ExecuteAsync($"CREATE DATABASE [{DatabaseName}];");

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
        }.ConnectionString;

        await using var conn = new SqlConnection(ConnectionString);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "001_create_alarms.sql"));
        foreach (var batch in GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
            await conn.ExecuteAsync(batch);

        await SeedData.InsertAsync(conn);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
