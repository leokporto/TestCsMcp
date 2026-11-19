using Scada.Alarms.Data.Breakers;

namespace Scada.Alarms.Data.Tests;

public class BreakerDowntimeCalculatorTests
{
    private static readonly DateTime From = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddHours(24);

    private static BreakerEvent Ev(long id, int minutesFromStart, string value) =>
        new(id, From.AddMinutes(minutesFromStart), value);

    [Fact]
    public void Single_open_close_cycle()
    {
        var result = BreakerDowntimeCalculator.Calculate(BreakerState.Closed,
            [Ev(1, 60, "1"), Ev(2, 90, "2")], From, To);

        Assert.Equal(TimeSpan.FromMinutes(30), result.TotalDowntime);
        var period = Assert.Single(result.OpenPeriods);
        Assert.False(period.StartClipped);
        Assert.False(period.EndClipped);
        Assert.False(result.OpenAtStart);
        Assert.False(result.OpenAtEnd);
    }

    [Fact]
    public void Repeated_opens_and_transitional_values_count_as_one_period()
    {
        var result = BreakerDowntimeCalculator.Calculate(null,
        [
            Ev(1, 10, "1"),
            Ev(2, 11, "0"),
            Ev(3, 12, "1"),
            Ev(4, 13, "3"),
            Ev(5, 14, "1"),
            Ev(6, 40, "2"),
        ], From, To);

        Assert.Equal(TimeSpan.FromMinutes(30), result.TotalDowntime);
        Assert.Single(result.OpenPeriods);
        Assert.Equal(2, result.EventsConsidered);
        Assert.Equal(4, result.EventsIgnored);
    }

    [Fact]
    public void Multiple_cycles_are_summed()
    {
        var result = BreakerDowntimeCalculator.Calculate(BreakerState.Closed,
            [Ev(1, 0, "1"), Ev(2, 10, "2"), Ev(3, 100, "1"), Ev(4, 120, "2")], From, To);

        Assert.Equal(TimeSpan.FromMinutes(30), result.TotalDowntime);
        Assert.Equal(2, result.OpenPeriods.Count);
    }

    [Fact]
    public void Open_before_window_is_clipped_at_start()
    {
        var result = BreakerDowntimeCalculator.Calculate(BreakerState.Open,
            [Ev(1, 45, "2")], From, To);

        Assert.Equal(TimeSpan.FromMinutes(45), result.TotalDowntime);
        Assert.True(result.OpenAtStart);
        Assert.True(Assert.Single(result.OpenPeriods).StartClipped);
    }

    [Fact]
    public void Still_open_at_end_is_clipped_at_end()
    {
        var result = BreakerDowntimeCalculator.Calculate(BreakerState.Closed,
            [Ev(1, 23 * 60, "1")], From, To);

        Assert.Equal(TimeSpan.FromHours(1), result.TotalDowntime);
        Assert.True(result.OpenAtEnd);
        Assert.True(Assert.Single(result.OpenPeriods).EndClipped);
    }

    [Fact]
    public void Open_for_whole_window_without_events()
    {
        var result = BreakerDowntimeCalculator.Calculate(BreakerState.Open, [], From, To);

        Assert.Equal(TimeSpan.FromHours(24), result.TotalDowntime);
        Assert.True(result.OpenAtStart);
        Assert.True(result.OpenAtEnd);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(BreakerState.Closed)]
    public void No_downtime_when_closed_or_unknown_and_no_events(BreakerState? before)
    {
        var result = BreakerDowntimeCalculator.Calculate(before, [], From, To);

        Assert.Equal(TimeSpan.Zero, result.TotalDowntime);
        Assert.Empty(result.OpenPeriods);
    }

    [Fact]
    public void Close_without_previous_open_is_ignored()
    {
        var result = BreakerDowntimeCalculator.Calculate(null,
            [Ev(1, 5, "2"), Ev(2, 10, "xyz"), Ev(3, 20, "1"), Ev(4, 25, "2")], From, To);

        Assert.Equal(TimeSpan.FromMinutes(5), result.TotalDowntime);
        Assert.Equal(2, result.EventsIgnored);
    }

    [Fact]
    public void Unordered_input_is_sorted_by_time()
    {
        var result = BreakerDowntimeCalculator.Calculate(null,
            [Ev(2, 30, "2"), Ev(1, 10, "1")], From, To);

        Assert.Equal(TimeSpan.FromMinutes(20), result.TotalDowntime);
    }

    [Fact]
    public void Rejects_empty_window()
    {
        Assert.Throws<ArgumentException>(() => BreakerDowntimeCalculator.Calculate(null, [], From, From));
    }
}
