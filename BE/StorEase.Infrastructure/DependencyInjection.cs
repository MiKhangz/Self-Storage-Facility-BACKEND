using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StorEase.Application.Interfaces;
using StorEase.Infrastructure.Persistence;
using StorEase.Infrastructure.Persistence.Seed;
using StorEase.Infrastructure.Services;

namespace StorEase.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string? connectionString, JwtOptions jwtOptions, string uploadsRoot)
    {
        // MigrationsAssembly: the DbContext lives here but the startup project is StorEase.Api.
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("StorEase.Infrastructure")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IEmailService, LoggingEmailService>();
        services.AddSingleton<IFileStorage>(new LocalFileStorage(uploadsRoot));
        services.AddScoped<DbSeeder>();
        return services;
    }
}
