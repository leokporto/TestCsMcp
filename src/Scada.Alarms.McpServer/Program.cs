using Scada.Alarms.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAlarmsData(builder.Configuration.GetConnectionString("AlarmHistorian") ?? "");

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run();

public partial class Program;
