using Microsoft.Extensions.DependencyInjection;
using Scada.Alarms.Data.Breakers;

namespace Scada.Alarms.Data;

public sealed record AlarmsDataOptions(string ConnectionString);

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlarmsData(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'AlarmHistorian' is not configured (ConnectionStrings:AlarmHistorian).");

        services.AddSingleton(new AlarmsDataOptions(connectionString));
        services.AddSingleton<IAlarmsRepository, SqlAlarmsRepository>();
        return services;
    }
}
