using ClaimsManagement.Domain.Common;

namespace ClaimsManagement.Domain.Entities;

public class ClaimAttachment : TenantEntity
{
    public Guid ClaimId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }

    public Claim Claim { get; set; } = null!;
}
