using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaimsManagement.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "Admin", "Employee", "Supervisor", "Finance" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole { Name = r });
                if (!result.Succeeded)
                    logger.LogWarning("Role {Role} not created: {Errors}", r, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (await db.Tenants.AnyAsync(t => t.Slug == "demo", cancellationToken))
            return;

        logger.LogInformation("Seeding demo tenant and catalog data.");

        var tenantId = Guid.NewGuid();
        var deptId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Demo Organization",
            Slug = "demo",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var department = new Department
        {
            Id = deptId,
            TenantId = tenantId,
            Name = "General",
            Code = "GEN",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var usdId = Guid.NewGuid();
        var zigId = Guid.NewGuid();

        var usd = new Currency { Id = usdId, Code = "USD", Name = "US Dollar", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow };
        var zig = new Currency { Id = zigId, Code = "ZWG", Name = "Zimbabwe Gold (ZiG)", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow };

        db.Tenants.Add(tenant);
        db.Departments.Add(department);
        db.Currencies.AddRange(usd, zig);
        db.ExchangeRates.Add(new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrencyId = usdId,
            ToCurrencyId = zigId,
            Rate = 26.5m,
            EffectiveFromUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var claimTypeSpecs = new (string Name, string Code)[]
        {
            ("Travel", "TRAVEL"),
            ("Subsistence", "SUBSIST"),
            ("Fuel", "FUEL"),
            ("Medical", "MEDICAL"),
            ("Procurement reimbursement", "PROCURE"),
            ("Project expense", "PROJECT"),
            ("Allowance", "ALLOW")
        };

        foreach (var (name, code) in claimTypeSpecs)
        {
            db.ClaimTypes.Add(new ClaimType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Code = code,
                FormSchemaJson = null,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        const string adminEmail = "admin@demo.local";
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = adminEmail,
            UserName = adminEmail,
            NormalizedEmail = adminEmail.ToUpperInvariant(),
            NormalizedUserName = adminEmail.ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = "Demo Admin",
            SecurityStamp = Guid.NewGuid().ToString("D"),
        };

        var createResult = await userManager.CreateAsync(admin, "Demo123!");
        if (!createResult.Succeeded)
        {
            logger.LogError("Could not seed admin user: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, "Admin");

        db.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = admin.Id,
            DepartmentId = deptId,
            FullName = admin.FullName,
            Email = adminEmail,
            EmployeeNumber = "E001",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        const string submitterEmail = "submitter@demo.local";
        var submitter = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = submitterEmail,
            UserName = submitterEmail,
            NormalizedEmail = submitterEmail.ToUpperInvariant(),
            NormalizedUserName = submitterEmail.ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = "Demo Submitter",
            SecurityStamp = Guid.NewGuid().ToString("D"),
        };

        var submitterResult = await userManager.CreateAsync(submitter, "Demo123!");
        if (!submitterResult.Succeeded)
        {
            logger.LogWarning(
                "Could not seed submitter user: {Errors}",
                string.Join(", ", submitterResult.Errors.Select(e => e.Description)));
        }
        else
        {
            await userManager.AddToRoleAsync(submitter, "Employee");
            db.Employees.Add(new Employee
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = submitter.Id,
                DepartmentId = deptId,
                FullName = submitter.FullName,
                Email = submitterEmail,
                EmployeeNumber = "E002",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Demo data seeded. Admin {Admin} / Demo123! · Submitter {Submitter} / Demo123!",
            adminEmail,
            submitterEmail);
    }
}
