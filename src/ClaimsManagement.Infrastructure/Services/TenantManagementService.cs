using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Services;

public interface ITenantManagementService
{
    Task<TenantDetails?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ApplicationUser> CreateTenantUserAsync(Guid tenantId, string email, string fullName, IEnumerable<string> roles, Guid? departmentId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DepartmentDetails>> GetDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Department> CreateDepartmentAsync(Guid tenantId, string code, string name, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<ClaimType> CreateClaimTypeAsync(Guid tenantId, string code, string name, string description, string? formSchemaJson, string[] workflowSteps, Guid createdByUserId, CancellationToken cancellationToken = default);
}

public record TenantDetails(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    int EmployeeCount,
    int DepartmentCount,
    DateTimeOffset CreatedAtUtc);

public record TenantUser(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public record DepartmentDetails(
    Guid Id,
    string Code,
    string Name,
    int EmployeeCount,
    bool IsActive);

public class TenantManagementService : ITenantManagementService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public TenantManagementService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<TenantDetails?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
            return null;

        var employeeCount = await _db.Employees.CountAsync(e => e.TenantId == tenantId, cancellationToken);
        var departmentCount = await _db.Departments.CountAsync(d => d.TenantId == tenantId, cancellationToken);

        return new TenantDetails(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            employeeCount,
            departmentCount,
            tenant.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.Employee)
            .Where(u => u.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var result = new List<TenantUser>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new TenantUser(
                user.Id,
                user.Email ?? "",
                user.Employee?.FullName ?? "",
                roles.ToList().AsReadOnly(),
                user.IsActive,
                user.CreatedAtUtc));
        }

        return result;
    }

    public async Task<ApplicationUser> CreateTenantUserAsync(
        Guid tenantId,
        string email,
        string fullName,
        IEnumerable<string> roles,
        Guid? departmentId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate tenant
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        // Validate department if provided
        if (departmentId.HasValue)
        {
            var department = await _db.Departments
                .FirstOrDefaultAsync(d => d.Id == departmentId.Value && d.TenantId == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Department not found or does not belong to this tenant.");
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
            throw new InvalidOperationException("A user with this email already exists.");

        // Create user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            TenantId = tenantId,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var tempPassword = GenerateTemporaryPassword();
        var result = await _userManager.CreateAsync(user, tempPassword);

        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        // Add roles
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = role,
                    NormalizedName = role.ToUpperInvariant(),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            await _userManager.AddToRoleAsync(user, role);
        }

        // Create employee record
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            FullName = fullName,
            Email = email,
            DepartmentId = departmentId,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(ApplicationUser),
            EntityId = user.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                email,
                fullName,
                roles,
                departmentId
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // TODO: Send welcome email with temporary password
        // For now, we'll just return the user (in production, you'd send the password via email)

        return user;
    }

    public async Task<IReadOnlyList<DepartmentDetails>> GetDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new DepartmentDetails(
                d.Id,
                d.Code,
                d.Name,
                _db.Employees.Count(e => e.DepartmentId == d.Id),
                d.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<Department> CreateDepartmentAsync(
        Guid tenantId,
        string code,
        string name,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate tenant
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        // Check for duplicate code
        var existing = await _db.Departments
            .AnyAsync(d => d.TenantId == tenantId && d.Code == code, cancellationToken);
        if (existing)
            throw new InvalidOperationException("A department with this code already exists.");

        var department = new Department
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Departments.Add(department);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(Department),
            EntityId = department.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new { code, name }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return department;
    }

    public async Task<ClaimType> CreateClaimTypeAsync(
        Guid tenantId,
        string code,
        string name,
        string description,
        string? formSchemaJson,
        string[] workflowSteps,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate tenant
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        // Check for duplicate code
        var existing = await _db.ClaimTypes
            .AnyAsync(ct => ct.TenantId == tenantId && ct.Code == code, cancellationToken);
        if (existing)
            throw new InvalidOperationException("A claim type with this code already exists.");

        var claimType = new ClaimType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = description,
            FormSchemaJson = formSchemaJson,
            WorkflowSteps = workflowSteps,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.ClaimTypes.Add(claimType);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(ClaimType),
            EntityId = claimType.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                code,
                name,
                description,
                hasFormSchema = !string.IsNullOrEmpty(formSchemaJson),
                workflowSteps
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return claimType;
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var random = new Random();
        var result = new char[12];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }
}
