namespace Scada.Alarms.Data.Breakers;

public interface IAlarmsRepository
{
    /// <summary>Last valid state (1 or 2) recorded strictly before <paramref name="fromUtc"/>, or null if none.</summary>
    Task<BreakerState?> GetLastStateBeforeAsync(BreakerTag tag, DateTime fromUtc, CancellationToken ct = default);

    /// <summary>All events of the tag in [fromUtc, toUtc), ordered by time then ID.</summary>
    Task<IReadOnlyList<BreakerEvent>> GetEventsAsync(BreakerTag tag, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Distinct breaker tags (TagName ending in .DJ), optionally filtered by a Level prefix.</summary>
    Task<IReadOnlyList<BreakerTag>> ListBreakersAsync(string? levelPrefix, CancellationToken ct = default);
}
