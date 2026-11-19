# SCADA Alarms MCP Server

A demo [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server built with .NET 11 and the [official C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore` 2.4.0) that lets an AI assistant answer questions like:

> "What was the downtime for SE_01's Bay_03 circuit breaker today?"

It reads the alarm history of a SCADA system (Action.Net `dbo.Alarms` table in SQL Server) and calculates how long a circuit breaker stayed open within a time window.

> This is a learning/demo project built against a simulated SCADA. It is not intended for production use.

## Tools

| Tool | Description |
|---|---|
| `list_breakers` | Lists circuit breaker tags (tag name ending in `.DJ`), optionally filtered by level, e.g. `REGIAO_01/SE_01`. |
| `get_breaker_downtime` | Returns how long a breaker stayed open in a `[start, end)` window, with each open period, clipped to the window. |

Business rules:
- Breaker state `1` = open, `2` = closed. States `0` and `3` (intermediate/invalid) are ignored.
- Repeated `1`s before a `2` count as a single open period (SCADA signals are noisy).
- Periods are clipped to the window. If the breaker was open before `start`, it counts from `start`. If it is still open at `end`, it counts until `end` and the result says so.

## Tech stack

- .NET 11, ASP.NET Core
- Official C# MCP SDK: [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) 2.4.0 (server, stateless Streamable HTTP) and `ModelContextProtocol` 2.4.0 (client, in the end-to-end test)
- Dapper + SQL Server
- xUnit v3 (Microsoft.Testing.Platform), Testcontainers

## Project structure

```
src/
  Scada.Alarms.McpServer/          ASP.NET Core host, MCP tools (Tools/BreakerTools.cs)
  Scada.Alarms.Data/               Repository (Dapper) and the downtime calculator
tests/
  Scada.Alarms.Data.Tests/         Unit tests (no external dependencies)
  Scada.Alarms.IntegrationTests/   SQL Server in Docker (Testcontainers) + end-to-end MCP client test
docker-compose.yml                 Local SQL Server for development
```

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for the local SQL Server and the integration tests)

## Getting started

### 1. Start SQL Server

Create a `.env` file in the repository root (it is git-ignored):

```env
COMPOSE_PROJECT_NAME=scada-alarms
MSSQL_SA_PASSWORD=<your-strong-password>
```

SQL Server requires a strong password: at least 8 characters, with three of these four: uppercase, lowercase, digits and symbols.

Then start the container:

```bash
docker compose up -d
```

SQL Server listens on `localhost:1433`. Data is kept in a named Docker volume, so it survives container restarts.

### 2. Database and alarm table

The server expects a database (for example `ActionNet`) that contains the Action.Net `dbo.Alarms` table. Normally Action.Net creates and fills this table when its alarm historian points to this SQL Server.

Without Action.Net, you can create an empty table with the script used by the integration tests:

```bash
docker exec -it scada-alarms_sqlsrv /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<your-strong-password>' -C -Q "CREATE DATABASE ActionNet"
docker exec -i scada-alarms_sqlsrv /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<your-strong-password>' -C -d ActionNet < tests/Scada.Alarms.IntegrationTests/Sql/001_create_alarms.sql
```

### 3. Configure the connection string (user secrets)

The connection string is **not** stored in the repository. `appsettings.json` leaves it empty, and in development it comes from .NET [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "ConnectionStrings:AlarmHistorian" "Server=localhost,1433;Database=ActionNet;User Id=sa;Password=<your-strong-password>;TrustServerCertificate=True" --project src/Scada.Alarms.McpServer
```

User secrets are loaded automatically when `ASPNETCORE_ENVIRONMENT=Development`, which is what `dotnet run` uses (see `Properties/launchSettings.json`). In other environments, set the environment variable `ConnectionStrings__AlarmHistorian` instead.

If the connection string is missing, the server fails at startup with `Connection string 'AlarmHistorian' is not configured`.

### 4. Run the server

```bash
dotnet run --project src/Scada.Alarms.McpServer
```

The MCP endpoint is `http://localhost:5100/mcp`.

### 5. Connect an MCP client

**Claude Code:**

```bash
claude mcp add --transport http alarms http://localhost:5100/mcp
```

Then ask: *"What was the downtime for SE_01's Bay_03 circuit breaker today?"*

**MCP Inspector:**

```bash
npx @modelcontextprotocol/inspector
```

Choose the **Streamable HTTP** transport and the URL `http://localhost:5100/mcp`.

## Running the tests

```bash
dotnet test --solution TestCsMcp.slnx                      # all tests (integration tests need Docker running)
dotnet test --project tests/Scada.Alarms.Data.Tests        # unit tests only
```

The integration tests start their own disposable SQL Server container with Testcontainers. They don't use the `docker-compose.yml` database or your user secrets.
