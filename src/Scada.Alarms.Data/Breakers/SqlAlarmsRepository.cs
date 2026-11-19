using Dapper;
using Microsoft.Data.SqlClient;

namespace Scada.Alarms.Data.Breakers;

/// <summary>Dapper access to dbo.Alarms. ActiveTime_Ticks are .NET ticks in UTC. Dapper sends strings as nvarchar, matching the columns.</summary>
public sealed class SqlAlarmsRepository(AlarmsDataOptions options) : IAlarmsRepository
{
    private const string LastStateBeforeSql = """
        SELECT TOP (1) Value
        FROM dbo.Alarms
        WHERE Level = @Level AND TagName = @TagName
          AND ActiveTime_Ticks < @FromTicks
          AND Value IN (N'1', N'2')
        ORDER BY ActiveTime_Ticks DESC, ID DESC;
        """;

    private const string EventsSql = """
        SELECT ID AS Id, ActiveTime_Ticks AS Ticks, Value
        FROM dbo.Alarms
        WHERE Level = @Level AND TagName = @TagName
          AND ActiveTime_Ticks >= @FromTicks AND ActiveTime_Ticks < @ToTicks
        ORDER BY ActiveTime_Ticks, ID;
        """;

    private const string ListBreakersSql = """
        SELECT DISTINCT Level, TagName
        FROM dbo.Alarms
        WHERE TagName LIKE N'%.DJ'
          AND (@LevelPrefix IS NULL OR Level LIKE @LevelPrefix + N'%')
        ORDER BY Level, TagName;
        """;

    public async Task<BreakerState?> GetLastStateBeforeAsync(BreakerTag tag, DateTime fromUtc, CancellationToken ct = default)
    {
        await using var conn = Open();
        var value = await conn.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
            LastStateBeforeSql,
            new { Level = tag.Level, TagName = tag.TagName, FromTicks = ToTicks(fromUtc) },
            cancellationToken: ct));

        return value is null ? null : new BreakerEvent(0, fromUtc, value).State;
    }

    public async Task<IReadOnlyList<BreakerEvent>> GetEventsAsync(BreakerTag tag, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var conn = Open();
        var rows = await conn.QueryAsync<EventRow>(new CommandDefinition(
            EventsSql,
            new
            {
                Level = tag.Level,
                TagName = tag.TagName,
                FromTicks = ToTicks(fromUtc),
                ToTicks = ToTicks(toUtc),
            },
            cancellationToken: ct));

        return rows.Select(r => new BreakerEvent(r.Id, new DateTime(r.Ticks, DateTimeKind.Utc), r.Value)).ToList();
    }

    public async Task<IReadOnlyList<BreakerTag>> ListBreakersAsync(string? levelPrefix, CancellationToken ct = default)
    {
        string? prefix = null;
        if (!string.IsNullOrWhiteSpace(levelPrefix))
        {
            var trimmed = levelPrefix.Trim();
            if (trimmed.StartsWith("Tag.", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[4..];
            prefix = "/" + trimmed.TrimStart('/');
        }

        await using var conn = Open();
        var rows = await conn.QueryAsync<(string Level, string TagName)>(new CommandDefinition(
            ListBreakersSql,
            new { LevelPrefix = prefix },
            cancellationToken: ct));

        return rows.Select(r => new BreakerTag(r.Level, r.TagName)).ToList();
    }

    private SqlConnection Open() => new(options.ConnectionString);

    private static long ToTicks(DateTime utc) =>
        (utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc).Ticks;

    private sealed record EventRow(long Id, long Ticks, string? Value);
}
