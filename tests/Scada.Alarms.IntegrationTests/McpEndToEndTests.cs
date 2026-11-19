using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Scada.Alarms.IntegrationTests;

/// <summary>
/// Hosts the real MCP server in memory (WebApplicationFactory) pointed at the Testcontainers SQL Server
/// and talks to it with the official MCP C# client over Streamable HTTP.
/// </summary>
public sealed class McpEndToEndTests(SqlServerFixture db) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private McpClient _client = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
            web.UseSetting("ConnectionStrings:AlarmHistorian", db.ConnectionString));

        var http = _factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http);

        _client = await McpClient.CreateAsync(transport, cancellationToken: Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Server_exposes_breaker_tools()
    {
        var tools = await _client.ListToolsAsync(cancellationToken: Ct);

        Assert.Equal(["get_breaker_downtime", "list_breakers"], tools.Select(t => t.Name).Order());
    }

    [Fact]
    public async Task Get_breaker_downtime_returns_expected_total()
    {
        var result = await _client.CallToolAsync("get_breaker_downtime", new Dictionary<string, object?>
        {
            ["tag"] = "Tag." + SeedData.FullPath,
            ["start"] = "2026-10-31T21:00:00-03:00", // = 2026-11-01T00:00Z
            ["end"] = "2026-11-01T21:00:00-03:00",
        }, cancellationToken: Ct);

        Assert.NotEqual(true, result.IsError);
        using var json = ParseText(result);
        var root = json.RootElement;
        Assert.Equal(SeedData.FullPath, root.GetProperty("tag").GetString());
        Assert.Equal(SeedData.ExpectedDowntime.TotalSeconds, root.GetProperty("totalDowntimeSeconds").GetDouble());
        Assert.Equal("0.01:50:00", root.GetProperty("totalDowntime").GetString());
        Assert.True(root.GetProperty("openAtStart").GetBoolean());
        Assert.True(root.GetProperty("openAtEnd").GetBoolean());
        Assert.Equal(3, root.GetProperty("openPeriods").GetArrayLength());
    }

    [Fact]
    public async Task Get_breaker_downtime_reports_invalid_tag_as_tool_error()
    {
        var result = await _client.CallToolAsync("get_breaker_downtime", new Dictionary<string, object?>
        {
            ["tag"] = "REGIAO_01/SE_01/BAY_03.STA.POS",
            ["start"] = "2026-11-01T00:00:00Z",
            ["end"] = "2026-11-02T00:00:00Z",
        }, cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Contains(".DJ", result.Content.OfType<TextContentBlock>().Single().Text);
    }

    [Fact]
    public async Task List_breakers_returns_seeded_tags()
    {
        var result = await _client.CallToolAsync("list_breakers", new Dictionary<string, object?>
        {
            ["levelPrefix"] = "REGIAO_01/SE_01",
        }, cancellationToken: Ct);

        using var json = ParseText(result);
        var tags = json.RootElement.GetProperty("tags").EnumerateArray().Select(e => e.GetString());
        Assert.Equal([SeedData.FullPath], tags);
    }

    private static JsonDocument ParseText(CallToolResult result) =>
        JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
}
