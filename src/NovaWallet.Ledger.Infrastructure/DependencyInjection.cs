using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Ledger.Domain.Interfaces;
using NovaWallet.Ledger.Infrastructure.Persistence;

namespace NovaWallet.Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var useSqlite = configuration.GetValue<bool>("UseSqlite");

        if (useSqlite)
        {
            // SQLite for local development (no external DB server needed)
            services.AddDbContext<LedgerDbContext>(options =>
                options.UseSqlite(connectionString));
        }
        else
        {
            // MySQL for production / Docker
            services.AddDbContext<LedgerDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        }

        // Repositories
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ILedgerTransactionRepository, LedgerTransactionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
