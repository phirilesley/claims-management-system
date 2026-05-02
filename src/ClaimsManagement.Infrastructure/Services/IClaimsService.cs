using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Infrastructure.Services;

public interface IClaimsService
{
    Task<Claim> CreateClaimAsync(
        Guid userId,
        Guid claimTypeId,
        string title,
        Guid currencyId,
        bool submit,
        string? dynamicDataJson,
        string? bankDetailsJson,
        IReadOnlyList<CreateClaimLineModel> lines,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Claim>> GetClaimsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record CreateClaimLineModel(
    int LineNumber,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    string? Category,
    decimal? MileageKm,
    decimal? PerDiemDays,
    string? MetadataJson);
