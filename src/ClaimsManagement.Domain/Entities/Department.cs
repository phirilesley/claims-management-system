using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class Department : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
