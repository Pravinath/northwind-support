using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        var sla = configuration.GetSection("Sla");
        var windows = sla.GetSection("BaseWindowHours").GetChildren()
            .Where(x => double.TryParse(x.Value, out _))
            .ToDictionary(x => x.Key, x => double.Parse(x.Value!), StringComparer.OrdinalIgnoreCase);
        services.AddSingleton(new TicketTriageOptions
        {
            BaseWindowHours = windows,
            PremiumCustomerMultiplier = double.TryParse(sla["PremiumCustomerMultiplier"], out var multiplier) ? multiplier : 0.5,
            MinimumWindowHours = double.TryParse(sla["MinimumWindowHours"], out var floor) ? floor : 1,
            AtRiskThresholdPercent = double.TryParse(sla["AtRiskThresholdPercent"], out var threshold) ? threshold : 25
        });

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketEscalationRepository, TicketEscalationRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ITicketTriageQueries, TicketTriageQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }
}
