using Dapper;
using Microsoft.Data.SqlClient;

namespace Scada.Alarms.IntegrationTests;

/// <summary>
/// Known scenario. Window = [2026-11-01 00:00 UTC, 2026-11-02 00:00 UTC) for /REGIAO_01/SE_01 BAY_03.STA.DJ:
///   31/10 23:00 = 1 (open before window)  -> counts 00:00..01:00 = 60 min (00:30=0 and 00:45=1 are noise)
///   02:00 = 1, 02:05 = 3, 02:10 = 1, 02:20 = 2 -> 20 min
///   23:30 = 1, no close until end            -> 30 min (clipped at end; the 2 at exactly 00:00 is outside)
///   Total = 110 min.
/// Same TagName under another Level and a non-DJ tag must not interfere.
/// </summary>
public static class SeedData
{
    public const string Level = "/REGIAO_01/SE_01";
    public const string TagName = "BAY_03.STA.DJ";
    public const string FullPath = "REGIAO_01/SE_01/BAY_03.STA.DJ";
    public static readonly DateTime WindowStartUtc = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime WindowEndUtc = WindowStartUtc.AddDays(1);
    public static readonly TimeSpan ExpectedDowntime = TimeSpan.FromMinutes(110);
    public const int ExpectedEventsInWindow = 8;

    private static readonly (string Level, string TagName, DateTime Utc, string Value)[] Rows =
    [
        (Level, TagName, Utc(2026, 10, 31, 22, 0), "2"),
        (Level, TagName, Utc(2026, 10, 31, 23, 0), "1"),
        (Level, TagName, Utc(2026, 11, 1, 0, 30), "0"),
        (Level, TagName, Utc(2026, 11, 1, 0, 45), "1"),
        (Level, TagName, Utc(2026, 11, 1, 1, 0), "2"),
        (Level, TagName, Utc(2026, 11, 1, 2, 0), "1"),
        (Level, TagName, Utc(2026, 11, 1, 2, 5), "3"),
        (Level, TagName, Utc(2026, 11, 1, 2, 10), "1"),
        (Level, TagName, Utc(2026, 11, 1, 2, 20), "2"),
        (Level, TagName, Utc(2026, 11, 1, 23, 30), "1"),
        (Level, TagName, Utc(2026, 11, 2, 0, 0), "2"),

        ("/REGIAO_01/SE_02", TagName, Utc(2026, 11, 1, 3, 0), "1"),
        ("/REGIAO_01/SE_02", TagName, Utc(2026, 11, 1, 9, 0), "2"),
        (Level, "BAY_03.STA.POS", Utc(2026, 11, 1, 4, 0), "1"),
        (Level, "BAY_04.STA.CHAVE", Utc(2026, 11, 1, 4, 0), "1"),
    ];

    public static async Task InsertAsync(SqlConnection conn)
    {
        const string sql = """
            INSERT INTO dbo.Alarms (Level, TagName, Value, ActiveTime_Ticks)
            VALUES (@Level, @TagName, @Value, @Ticks);
            """;

        foreach (var r in Rows)
            await conn.ExecuteAsync(sql, new { r.Level, r.TagName, r.Value, Ticks = r.Utc.Ticks });
    }

    private static DateTime Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);
}
