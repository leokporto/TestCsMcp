using Scada.Alarms.Data;
using Scada.Alarms.Data.Breakers;

namespace Scada.Alarms.IntegrationTests;

public class SqlAlarmsRepositoryTests(SqlServerFixture db)
{
    private readonly SqlAlarmsRepository _repo = new(new AlarmsDataOptions(db.ConnectionString));
    private static readonly BreakerTag Tag = BreakerTag.Parse(SeedData.FullPath);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Last_state_before_window_is_open()
    {
        var state = await _repo.GetLastStateBeforeAsync(Tag, SeedData.WindowStartUtc, Ct);

        Assert.Equal(BreakerState.Open, state);
    }

    [Fact]
    public async Task Last_state_is_null_when_no_history()
    {
        var state = await _repo.GetLastStateBeforeAsync(Tag, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), Ct);

        Assert.Null(state);
    }

    [Fact]
    public async Task Events_are_filtered_by_level_tag_and_window_and_ordered()
    {
        var events = await _repo.GetEventsAsync(Tag, SeedData.WindowStartUtc, SeedData.WindowEndUtc, Ct);

        Assert.Equal(SeedData.ExpectedEventsInWindow, events.Count);
        Assert.All(events, e =>
        {
            Assert.InRange(e.TimestampUtc, SeedData.WindowStartUtc, SeedData.WindowEndUtc.AddTicks(-1));
            Assert.Equal(DateTimeKind.Utc, e.TimestampUtc.Kind);
        });
        Assert.Equal(events.OrderBy(e => e.TimestampUtc).Select(e => e.Id), events.Select(e => e.Id));
    }

    [Fact]
    public async Task Repository_and_calculator_give_expected_downtime()
    {
        var before = await _repo.GetLastStateBeforeAsync(Tag, SeedData.WindowStartUtc, Ct);
        var events = await _repo.GetEventsAsync(Tag, SeedData.WindowStartUtc, SeedData.WindowEndUtc, Ct);

        var result = BreakerDowntimeCalculator.Calculate(before, events, SeedData.WindowStartUtc, SeedData.WindowEndUtc);

        Assert.Equal(SeedData.ExpectedDowntime, result.TotalDowntime);
        Assert.True(result.OpenAtStart);
        Assert.True(result.OpenAtEnd);
        Assert.Equal(3, result.OpenPeriods.Count);
    }

    [Fact]
    public async Task List_breakers_returns_only_dj_tags()
    {
        var all = await _repo.ListBreakersAsync(null, Ct);

        Assert.Equal(["REGIAO_01/SE_01/BAY_03.STA.DJ", "REGIAO_01/SE_02/BAY_03.STA.DJ"], all.Select(t => t.FullPath));
    }

    [Theory]
    [InlineData("REGIAO_01/SE_02")]
    [InlineData("/REGIAO_01/SE_02")]
    [InlineData("Tag.REGIAO_01/SE_02")]
    public async Task List_breakers_filters_by_level_prefix(string prefix)
    {
        var filtered = await _repo.ListBreakersAsync(prefix, Ct);

        Assert.Equal("REGIAO_01/SE_02/BAY_03.STA.DJ", Assert.Single(filtered).FullPath);
    }
}
