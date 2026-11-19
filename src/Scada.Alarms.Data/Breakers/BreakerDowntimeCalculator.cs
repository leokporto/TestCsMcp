namespace Scada.Alarms.Data.Breakers;

public sealed record OpenPeriod(DateTime StartUtc, DateTime EndUtc, bool StartClipped, bool EndClipped)
{
    public TimeSpan Duration => EndUtc - StartUtc;
}

public sealed record BreakerDowntimeResult(
    TimeSpan TotalDowntime,
    IReadOnlyList<OpenPeriod> OpenPeriods,
    bool OpenAtStart,
    bool OpenAtEnd,
    int EventsConsidered,
    int EventsIgnored);

/// <summary>
/// Computes how long a breaker stayed open (Value = 1) inside [fromUtc, toUtc).
/// Only 1 (open) and 2 (closed) change state; 0, 3 and unparsable values are ignored.
/// Periods are clipped to the window.
/// </summary>
public static class BreakerDowntimeCalculator
{
    public static BreakerDowntimeResult Calculate(
        BreakerState? stateBeforeWindow,
        IEnumerable<BreakerEvent> eventsInWindow,
        DateTime fromUtc,
        DateTime toUtc)
    {
        if (toUtc <= fromUtc)
            throw new ArgumentException("End of the window must be after its start.", nameof(toUtc));

        var periods = new List<OpenPeriod>();
        var openAtStart = stateBeforeWindow == BreakerState.Open;
        DateTime? openSince = openAtStart ? fromUtc : null;
        var openSinceClipped = openAtStart;
        int considered = 0, ignored = 0;

        foreach (var ev in eventsInWindow.OrderBy(e => e.TimestampUtc).ThenBy(e => e.Id))
        {
            // Defensive: callers should only pass events inside the window.
            if (ev.TimestampUtc < fromUtc || ev.TimestampUtc >= toUtc)
                continue;

            switch (ev.State)
            {
                case BreakerState.Open when openSince is null:
                    openSince = ev.TimestampUtc;
                    openSinceClipped = false;
                    considered++;
                    break;

                case BreakerState.Closed when openSince is not null:
                    periods.Add(new OpenPeriod(openSince.Value, ev.TimestampUtc, openSinceClipped, EndClipped: false));
                    openSince = null;
                    considered++;
                    break;

                default:
                    // Repeated 1 while open, 2 while closed, 0, 3 or garbage: no state change.
                    ignored++;
                    break;
            }
        }

        var openAtEnd = openSince is not null;
        if (openAtEnd)
            periods.Add(new OpenPeriod(openSince!.Value, toUtc, openSinceClipped, EndClipped: true));

        var total = periods.Aggregate(TimeSpan.Zero, (acc, p) => acc + p.Duration);
        return new BreakerDowntimeResult(total, periods, openAtStart, openAtEnd, considered, ignored);
    }
}
