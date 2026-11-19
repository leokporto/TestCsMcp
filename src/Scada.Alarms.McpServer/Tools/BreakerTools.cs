using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scada.Alarms.Data.Breakers;

namespace Scada.Alarms.McpServer.Tools;

[McpServerToolType]
public sealed class BreakerTools(IAlarmsRepository repository, ILogger<BreakerTools> logger)
{
    [McpServerTool(Name = "get_breaker_downtime", Title = "Breaker downtime", ReadOnly = true, UseStructuredContent = true)]
    [Description("""
        Returns how long a circuit breaker (disjuntor, tag ending in '.DJ') stayed OPEN/OFF (desligado) within a time window,
        read from the SCADA alarm history. Value 1 = open/off, 2 = closed/on; 0 and 3 are transitional/invalid and ignored.
        Periods are clipped to the window: if the breaker was already open before 'start' it counts from 'start';
        if still open at 'end' it counts until 'end' and 'openAtEnd' is true.
        """)]
    public async Task<BreakerDowntimeResponse> GetBreakerDowntime(
        [Description("Breaker tag path, e.g. 'REGIAO_01/SE_01/BAY_03.STA.DJ' (the 'Tag.' prefix is optional). Use list_breakers to discover tags.")]
        string tag,
        [Description("Window start, ISO 8601 with UTC offset, e.g. '2026-11-01T00:00:00-03:00'.")]
        DateTimeOffset start,
        [Description("Window end (exclusive), ISO 8601 with UTC offset, e.g. '2026-11-02T00:00:00-03:00'.")]
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        if (end <= start)
            throw new McpException("'end' must be after 'start'.");

        BreakerTag breaker;
        try
        {
            breaker = BreakerTag.Parse(tag);
        }
        catch (ArgumentException ex)
        {
            throw new McpException(ex.Message);
        }

        var fromUtc = start.UtcDateTime;
        var toUtc = end.UtcDateTime;

        var stateBefore = await repository.GetLastStateBeforeAsync(breaker, fromUtc, cancellationToken);
        var events = await repository.GetEventsAsync(breaker, fromUtc, toUtc, cancellationToken);
        var result = BreakerDowntimeCalculator.Calculate(stateBefore, events, fromUtc, toUtc);

        logger.LogInformation("Downtime for {Tag} in [{From:o}, {To:o}): {Total} ({Events} events)",
            breaker.FullPath, fromUtc, toUtc, result.TotalDowntime, events.Count);

        var warnings = new List<string>();
        if (events.Count == 0 && stateBefore is null)
            warnings.Add("No events found for this tag in or before the window. Check the tag name with list_breakers.");
        if (result.OpenAtStart)
            warnings.Add("Breaker was already open before the window start; counted from 'start'.");
        if (result.OpenAtEnd)
            warnings.Add("Breaker was still open at the window end; counted until 'end' (cycle not closed).");

        return new BreakerDowntimeResponse(
            Tag: breaker.FullPath,
            StartUtc: fromUtc,
            EndUtc: toUtc,
            TotalDowntimeSeconds: Math.Round(result.TotalDowntime.TotalSeconds, 3),
            TotalDowntime: result.TotalDowntime.ToString(@"d\.hh\:mm\:ss"),
            OpenAtStart: result.OpenAtStart,
            OpenAtEnd: result.OpenAtEnd,
            EventsInWindow: events.Count,
            EventsIgnored: result.EventsIgnored,
            OpenPeriods: result.OpenPeriods
                .Select(p => new OpenPeriodDto(p.StartUtc, p.EndUtc, Math.Round(p.Duration.TotalSeconds, 3), p.StartClipped, p.EndClipped))
                .ToList(),
            Warnings: warnings);
    }

    [McpServerTool(Name = "list_breakers", Title = "List breakers", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists circuit breaker tags (TagName ending in '.DJ') found in the SCADA alarm history, optionally filtered by level prefix.")]
    public async Task<ListBreakersResponse> ListBreakers(
        [Description("Optional level prefix to filter, e.g. 'REGIAO_01' or 'REGIAO_01/SE_01'. Omit to list all.")]
        string? levelPrefix,
        CancellationToken cancellationToken)
    {
        var breakers = await repository.ListBreakersAsync(levelPrefix, cancellationToken);
        return new ListBreakersResponse(breakers.Select(b => b.FullPath).ToList());
    }
}

public sealed record OpenPeriodDto(DateTime StartUtc, DateTime EndUtc, double DurationSeconds, bool StartClipped, bool EndClipped);

public sealed record BreakerDowntimeResponse(
    string Tag,
    DateTime StartUtc,
    DateTime EndUtc,
    double TotalDowntimeSeconds,
    string TotalDowntime,
    bool OpenAtStart,
    bool OpenAtEnd,
    int EventsInWindow,
    int EventsIgnored,
    IReadOnlyList<OpenPeriodDto> OpenPeriods,
    IReadOnlyList<string> Warnings);

public sealed record ListBreakersResponse(IReadOnlyList<string> Tags);
