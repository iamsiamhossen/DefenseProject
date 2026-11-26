using KYAUCPChapter.Infrastructure.Identity;
using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KYAUCPChapter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseMySQL(connectionString));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ContestParticipationService>();
        services.AddScoped<ContestManagementService>();
        services.AddScoped<ContestQueries>();
        services.AddScoped<ProblemManagementService>();
        services.AddScoped<ProblemQueries>();
        services.AddScoped<AccountService>();
        return services;
    }
}
