using System.Globalization;

namespace Scada.Alarms.Data.Breakers;

/// <summary>Double-point state of a breaker (DJ) tag.</summary>
public enum BreakerState
{
    Intermediate = 0,
    Open = 1,    // desligado
    Closed = 2,  // ligado
    Invalid = 3,
}

/// <summary>One row of dbo.Alarms for a breaker tag.</summary>
public sealed record BreakerEvent(long Id, DateTime TimestampUtc, string? RawValue)
{
    /// <summary>Parsed state, or null when Value is not a known integer state.</summary>
    public BreakerState? State =>
        int.TryParse(RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
        && Enum.IsDefined(typeof(BreakerState), v)
            ? (BreakerState)v
            : null;
}
