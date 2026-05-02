using ClaimsManagement.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ClaimsManagement.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Employee? Employee { get; set; }
}
