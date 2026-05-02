using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class Employee : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public Department? Department { get; set; }
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
}
