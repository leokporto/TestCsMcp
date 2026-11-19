# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A test/learning project for the C# MCP SDK (`ModelContextProtocol` 2.4.0). It is an HTTP MCP server that lets an LLM ask how long a SCADA circuit breaker (tag ending in `.DJ`) stayed open within a time window, reading the Action.Net `dbo.Alarms` table (SQL Server). The solution file is `TestCsMcp.slnx` (XML `.slnx` format). Full write-up, in Portuguese: `docs/MCP-Disjuntores.md`.

- `src/Scada.Alarms.McpServer`: ASP.NET Core (`Microsoft.NET.Sdk.Web`) host using `ModelContextProtocol.AspNetCore`. It uses Streamable HTTP, **stateless**, mapped at `/mcp` on `http://localhost:5100`. Tools are discovered by reflection (`WithToolsFromAssembly()`): any class marked `[McpServerToolType]` whose methods are marked `[McpServerTool(Name = ...)]`. Tool classes are non-static and get dependencies through constructor DI, with one instance per call. Throw `McpException` to return an `isError` tool result. The `[Description]` text is what the LLM reads. Return DTOs live next to the tools and are the public contract (`UseStructuredContent = true` publishes an `outputSchema`).
- `src/Scada.Alarms.Data`:
  - `IAlarmsRepository` / `SqlAlarmsRepository` (Dapper).
  - `BreakerDowntimeCalculator`: a pure state machine, where all business rules live.
  - `BreakerTag`: path parsing.
  - Registered via `AddAlarmsData(connectionString)`, which reads `ConnectionStrings:AlarmHistorian`.
- `tests/Scada.Alarms.Data.Tests`: unit tests, no external dependencies.
- `tests/Scada.Alarms.IntegrationTests`: Testcontainers SQL Server, shared through an xUnit v3 `AssemblyFixture`. It creates the table from `Sql/001_create_alarms.sql` (a simplified `dbo.Alarms`: only the columns used, no secondary indexes) and seeds the scenario in `SeedData.cs`. It also runs an end-to-end test (`WebApplicationFactory<Program>` + the real `McpClient` over `HttpClientTransport`). Requires Docker.

## Domain rules (not obvious from code alone)

- **Tag storage.** `Tag.REGIAO_01/SE_01/BAY_03.STA.DJ` is stored in `dbo.Alarms` as `Level = '/REGIAO_01/SE_01'` (leading slash) and `TagName = 'BAY_03.STA.DJ'`.
- **Breaker states.** `Value` is nvarchar and holds a double-point state: `1` = open/off, `2` = closed/on. `0` and `3` are intermediate/invalid and ignored. Repeated `1`s before a `2` count as one period, because the SCADA simulation is noisy.
- **Time window.** Windows are `[start, end)` in UTC and are compared against `ActiveTime_Ticks`, which hold .NET ticks in UTC.
- **Clipping at the window edges.** Periods are clipped to the window. The last valid state before `start` decides whether the breaker was already open at `start`.
- SCADA background (tags, levels, alarm historian) is in `.claude/skills/`.

## Commands

```bash
dotnet build TestCsMcp.slnx
dotnet run --project src/Scada.Alarms.McpServer           # http://localhost:5100/mcp, Development env
dotnet test --solution TestCsMcp.slnx                          # all tests (integration ones need Docker)
dotnet test --project tests/Scada.Alarms.Data.Tests       # unit tests only
```

`global.json` selects **Microsoft.Testing.Platform**, and the test projects use `xunit.v3.mtp-v2`. Use the MTP syntax (`--project` / `--solution`); the VSTest `--filter` option does not apply. To run a single test:

```bash
dotnet test --project tests/Scada.Alarms.Data.Tests --filter-method "*Repeated_opens*"
dotnet test --project tests/Scada.Alarms.IntegrationTests --filter-class "Scada.Alarms.IntegrationTests.McpEndToEndTests"
```

The dev connection string is kept in .NET **user secrets** (never in the repo; `appsettings.json` has it empty) and points to the local SQL Server in Docker on `localhost:1433`, database `ActionNet`. Set it once with:

```bash
dotnet user-secrets set "ConnectionStrings:AlarmHistorian" "Server=localhost,1433;Database=ActionNet;User Id=sa;Password=<password>;TrustServerCertificate=True" --project src/Scada.Alarms.McpServer
```

Override it with the env var `ConnectionStrings__AlarmHistorian`.

Manual check: `npx @modelcontextprotocol/inspector` (Streamable HTTP, `http://localhost:5100/mcp`), or `claude mcp add --transport http alarms http://localhost:5100/mcp`.
