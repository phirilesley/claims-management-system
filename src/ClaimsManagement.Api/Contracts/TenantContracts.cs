namespace ClaimsManagement.Api.Contracts;

public record TenantDetailsResponse(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    int EmployeeCount,
    int DepartmentCount,
    DateTimeOffset CreatedAtUtc);

public record TenantUserResponse(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public record CreateTenantUserRequest(
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    Guid? DepartmentId);

public record DepartmentResponse(
    Guid Id,
    string Code,
    string Name,
    int EmployeeCount,
    bool IsActive);

public record CreateDepartmentRequest(
    string Code,
    string Name);

public record ClaimTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string? FormSchemaJson,
    string[] WorkflowSteps);

public record CreateClaimTypeRequest(
    string Code,
    string Name,
    string Description,
    string? FormSchemaJson,
    string[] WorkflowSteps);
