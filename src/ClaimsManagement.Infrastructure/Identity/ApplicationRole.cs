using Microsoft.AspNetCore.Identity;

namespace ClaimsManagement.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
