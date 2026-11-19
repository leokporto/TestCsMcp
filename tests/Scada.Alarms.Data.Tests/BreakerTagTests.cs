using Scada.Alarms.Data.Breakers;

namespace Scada.Alarms.Data.Tests;

public class BreakerTagTests
{
    [Theory]
    [InlineData("Tag.REGIAO_01/SE_01/BAY_03.STA.DJ")]
    [InlineData("REGIAO_01/SE_01/BAY_03.STA.DJ")]
    [InlineData("/REGIAO_01/SE_01/BAY_03.STA.DJ")]
    [InlineData("  tag.REGIAO_01/SE_01/BAY_03.STA.DJ  ")]
    public void Parse_splits_level_and_tag_name(string input)
    {
        var tag = BreakerTag.Parse(input);

        Assert.Equal("/REGIAO_01/SE_01", tag.Level);
        Assert.Equal("BAY_03.STA.DJ", tag.TagName);
        Assert.Equal("REGIAO_01/SE_01/BAY_03.STA.DJ", tag.FullPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BAY_03.STA.DJ")]                    // no level
    [InlineData("REGIAO_01/SE_01/")]                 // no tag name
    [InlineData("REGIAO_01/SE_01/BAY_03.STA.POS")]   // not a breaker
    public void Parse_rejects_invalid_paths(string input)
    {
        Assert.Throws<ArgumentException>(() => BreakerTag.Parse(input));
    }

    [Theory]
    [InlineData("1", BreakerState.Open)]
    [InlineData("2", BreakerState.Closed)]
    [InlineData("0", BreakerState.Intermediate)]
    [InlineData("3", BreakerState.Invalid)]
    [InlineData("7", null)]
    [InlineData("abc", null)]
    [InlineData(null, null)]
    public void Event_state_is_parsed_from_raw_value(string? raw, BreakerState? expected)
    {
        Assert.Equal(expected, new BreakerEvent(1, DateTime.UtcNow, raw).State);
    }
}
