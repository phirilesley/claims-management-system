using ClosedXML.Excel;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Services;

public interface IPaymentBatchService
{
    Task<IReadOnlyList<PaymentBatchSummary>> GetPaymentBatchesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PaymentBatch> CreatePaymentBatchAsync(Guid tenantId, IReadOnlyList<Guid> claimIds, Guid currencyId, string description, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<PaymentBatch> ProcessPaymentBatchAsync(Guid tenantId, Guid batchId, Guid processedByUserId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportPaymentBatchAsync(Guid tenantId, Guid batchId, string format, CancellationToken cancellationToken = default);
}

public record PaymentBatchSummary(
    Guid Id,
    string Reference,
    string Status,
    decimal TotalAmount,
    string CurrencyCode,
    int PaymentCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ProcessedAtUtc);

public class PaymentBatchService : IPaymentBatchService
{
    private readonly ApplicationDbContext _db;

    public PaymentBatchService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PaymentBatchSummary>> GetPaymentBatchesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.PaymentBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new PaymentBatchSummary(
                b.Id,
                b.Reference,
                b.Status.ToString(),
                b.TotalAmount,
                b.Currency.Code,
                b.Payments.Count,
                b.CreatedAtUtc,
                b.ProcessedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentBatch> CreatePaymentBatchAsync(
        Guid tenantId,
        IReadOnlyList<Guid> claimIds,
        Guid currencyId,
        string description,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate claims
        var claims = await _db.Claims
            .Include(c => c.Currency)
            .Where(c => claimIds.Contains(c.Id) && c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (claims.Count != claimIds.Count)
            throw new InvalidOperationException("Some claims were not found or do not belong to this tenant.");

        // Validate all claims are approved and not yet paid
        var invalidClaims = claims.Where(c => c.Status != ClaimStatus.Approved || c.Payments.Any(p => p.PaymentBatch != null)).ToList();
        if (invalidClaims.Any())
            throw new InvalidOperationException($"Only approved claims without existing payments can be batched. Found {invalidClaims.Count} invalid claims.");

        var currency = await _db.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == currencyId && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Currency is not available.");

        // Generate batch reference
        var reference = await GenerateBatchReferenceAsync(tenantId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var batch = new PaymentBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Reference = reference,
            Description = description,
            CurrencyId = currencyId,
            Status = PaymentBatchStatus.Draft,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            Payments = new List<ClaimPayment>()
        };

        decimal totalAmount = 0;
        foreach (var claim in claims)
        {
            // Convert to batch currency if needed
            var convertedAmount = claim.CurrencyId == currencyId 
                ? claim.TotalAmount 
                : await ConvertCurrencyAsync(claim.TotalAmount, claim.CurrencyId, currencyId, cancellationToken);

            totalAmount += convertedAmount;

            batch.Payments.Add(new ClaimPayment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ClaimId = claim.Id,
                Amount = convertedAmount,
                OriginalAmount = claim.TotalAmount,
                OriginalCurrencyId = claim.CurrencyId,
                Status = PaymentStatus.Pending,
                CreatedAtUtc = now
            });
        }

        batch.TotalAmount = totalAmount;
        batch.PaymentCount = batch.Payments.Count;

        _db.PaymentBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties for return
        await _db.Entry(batch).Reference(b => b.Currency).LoadAsync(cancellationToken);
        return batch;
    }

    public async Task<PaymentBatch> ProcessPaymentBatchAsync(
        Guid tenantId,
        Guid batchId,
        Guid processedByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var batch = await _db.PaymentBatches
            .Include(b => b.Payments)
                .ThenInclude(p => p.Claim)
            .Include(b => b.Currency)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Payment batch not found.");

        if (batch.Status != PaymentBatchStatus.Draft)
            throw new InvalidOperationException("Only draft batches can be processed.");

        var now = DateTimeOffset.UtcNow;
        batch.Status = PaymentBatchStatus.Processed;
        batch.ProcessedAtUtc = now;
        batch.ProcessedByUserId = processedByUserId;

        // Update all payments
        foreach (var payment in batch.Payments)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAtUtc = now;
            payment.PaymentReference = $"{batch.Reference}-PAY-{payment.Id:N[..8].ToUpper()}";

            // Update claim status
            payment.Claim.Status = ClaimStatus.Paid;
            payment.Claim.ModifiedAtUtc = now;

            // Add status history
            payment.Claim.StatusHistory.Add(new ClaimStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ClaimId = payment.ClaimId,
                FromStatus = ClaimStatus.Approved,
                ToStatus = ClaimStatus.Paid,
                ChangedByUserId = processedByUserId,
                ChangedAtUtc = now,
                Reason = $"Paid via batch {batch.Reference}",
                CreatedAtUtc = now
            });
        }

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(PaymentBatch),
            EntityId = batch.Id,
            Action = "Process",
            UserId = processedByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                batchId = batch.Id,
                reference = batch.Reference,
                paymentCount = batch.Payments.Count,
                totalAmount = batch.TotalAmount
            }),
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return batch;
    }

    public async Task<byte[]> ExportPaymentBatchAsync(
        Guid tenantId,
        Guid batchId,
        string format,
        CancellationToken cancellationToken = default)
    {
        var batch = await _db.PaymentBatches
            .AsNoTracking()
            .Include(b => b.Payments)
                .ThenInclude(p => p.Claim)
                    .ThenInclude(c => c.Employee)
            .Include(b => b.Currency)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Payment batch not found.");

        return format.ToLower() switch
        {
            "csv" => await ExportToCsvAsync(batch, cancellationToken),
            "xlsx" => await ExportToExcelAsync(batch, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported export format: {format}")
        };
    }

    private async Task<byte[]> ExportToCsvAsync(PaymentBatch batch, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);

        // CSV Header
        await writer.WriteLineAsync("Payment Reference,Claim Reference,Employee Name,Amount,Currency,Payment Date,Status");

        // CSV Data
        foreach (var payment in batch.Payments.OrderBy(p => p.PaymentReference))
        {
            var line = $"{payment.PaymentReference}," +
                       $"{payment.Claim.ReferenceNumber}," +
                       $"\"{payment.Claim.Employee.FullName}\"," +
                       $"{payment.Amount}," +
                       $"{batch.Currency.Code}," +
                       $"{payment.PaidAtUtc:yyyy-MM-dd}," +
                       $"{payment.Status}";

            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync(cancellationToken);
        return stream.ToArray();
    }

    private async Task<byte[]> ExportToExcelAsync(PaymentBatch batch, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add($"Payment Batch {batch.Reference}");

        // Headers
        worksheet.Cell(1, 1).Value = "Payment Reference";
        worksheet.Cell(1, 2).Value = "Claim Reference";
        worksheet.Cell(1, 3).Value = "Employee Name";
        worksheet.Cell(1, 4).Value = "Amount";
        worksheet.Cell(1, 5).Value = "Currency";
        worksheet.Cell(1, 6).Value = "Payment Date";
        worksheet.Cell(1, 7).Value = "Status";

        // Data
        for (int i = 0; i < batch.Payments.Count; i++)
        {
            var payment = batch.Payments.OrderBy(p => p.PaymentReference).ElementAt(i);
            var row = i + 2;

            worksheet.Cell(row, 1).Value = payment.PaymentReference;
            worksheet.Cell(row, 2).Value = payment.Claim.ReferenceNumber;
            worksheet.Cell(row, 3).Value = payment.Claim.Employee.FullName;
            worksheet.Cell(row, 4).Value = payment.Amount;
            worksheet.Cell(row, 5).Value = batch.Currency.Code;
            worksheet.Cell(row, 6).Value = payment.PaidAtUtc?.ToString("yyyy-MM-dd") ?? "";
            worksheet.Cell(row, 7).Value = payment.Status.ToString();
        }

        // Format
        worksheet.Columns().AdjustToContents();
        worksheet.Range(1, 1, 1, 7).Style.Font.Bold = true;
        worksheet.Range(1, 1, batch.Payments.Count + 1, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(1, 1, batch.Payments.Count + 1, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<string> GenerateBatchReferenceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PB-{year}-";
        var count = await _db.PaymentBatches.CountAsync(
            b => b.TenantId == tenantId && b.Reference.StartsWith(prefix),
            cancellationToken);
        return $"{prefix}{(count + 1):D5}";
    }

    private async Task<decimal> ConvertCurrencyAsync(
        decimal amount, 
        Guid fromCurrencyId, 
        Guid toCurrencyId, 
        CancellationToken cancellationToken)
    {
        if (fromCurrencyId == toCurrencyId)
            return amount;

        var rate = await _db.ExchangeRates
            .AsNoTracking()
            .Where(r => r.FromCurrencyId == fromCurrencyId && r.ToCurrencyId == toCurrencyId)
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (rate == null)
            throw new InvalidOperationException($"Exchange rate from currency {fromCurrencyId} to {toCurrencyId} not found.");

        return amount * rate.Rate;
    }
}
