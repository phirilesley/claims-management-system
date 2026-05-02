using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Persistence;
using ClaimsManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClaimsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
            }));

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IClaimsService, ClaimsService>();
        services.AddScoped<IClaimWorkflowService, ClaimWorkflowService>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddScoped<IPaymentBatchService, PaymentBatchService>();
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IRateManagementService, RateManagementService>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<IBankingService, BankingService>();
        
        // File storage - use local storage for MVP, switch to cloud for production
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
