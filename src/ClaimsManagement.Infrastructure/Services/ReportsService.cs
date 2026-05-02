using ClosedXML.Excel;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Services;

public interface IReportsService
{
    Task<byte[]> GenerateClaimsReportAsync(
        Guid tenantId,
        DateTime? from,
        DateTime? to,
        Guid? departmentId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateClaimsPdfReportAsync(
        Guid tenantId,
        DateTime? from,
        DateTime? to,
        Guid? departmentId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<byte[]> GeneratePaymentsReportAsync(
        Guid tenantId,
        DateTime? from,
        DateTime? to,
        Guid? batchId,
        CancellationToken cancellationToken = default);
}

public class ReportsService : IReportsService
{
    private readonly ApplicationDbContext _db;

    public ReportsService(ApplicationDbContext db)
    {
        _db = db;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateClaimsReportAsync(
        Guid tenantId,
        DateTime? from,
        DateTime? to,
        Guid? departmentId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var claims = await GetClaimsQuery(tenantId, from, to, departmentId, status)
            .Include(c => c.Employee)
                .ThenInclude(e => e.Department)
            .Include(c => c.ClaimType)
            .Include(c => c.Currency)
            .OrderBy(c => c.ReferenceNumber)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Claims Report");

        // Headers
        worksheet.Cell(1, 1).Value = "Reference";
        worksheet.Cell(1, 2).Value = "Title";
        worksheet.Cell(1, 3).Value = "Employee";
        worksheet.Cell(1, 4).Value = "Department";
        worksheet.Cell(1, 5).Value = "Claim Type";
        worksheet.Cell(1, 6).Value = "Status";
        worksheet.Cell(1, 7).Value = "Currency";
        worksheet.Cell(1, 8).Value = "Total Amount";
        worksheet.Cell(1, 9).Value = "Submitted Date";
        worksheet.Cell(1, 10).Value = "Created Date";

        // Data
        for (int i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            var row = i + 2;
            
            worksheet.Cell(row, 1).Value = claim.ReferenceNumber;
            worksheet.Cell(row, 2).Value = claim.Title;
            worksheet.Cell(row, 3).Value = claim.Employee?.FullName ?? "";
            worksheet.Cell(row, 4).Value = claim.Employee?.Department?.Name ?? "";
            worksheet.Cell(row, 5).Value = claim.ClaimType?.Name ?? "";
            worksheet.Cell(row, 6).Value = claim.Status.ToString();
            worksheet.Cell(row, 7).Value = claim.Currency?.Code ?? "";
            worksheet.Cell(row, 8).Value = claim.TotalAmount;
            worksheet.Cell(row, 9).Value = claim.SubmittedAtUtc?.DateTime.ToString("yyyy-MM-dd") ?? "";
            worksheet.Cell(row, 10).Value = claim.CreatedAtUtc.DateTime.ToString("yyyy-MM-dd");
        }

        // Format
        worksheet.Columns().AdjustToContents();
        worksheet.Range(1, 1, 1, 10).Style.Font.Bold = true;
        worksheet.Range(1, 1, claims.Count + 1, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(1, 1, claims.Count + 1, 10).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> GenerateClaimsPdfReportAsync(
        Guid tenantId,
        DateTime? from,
        DateTime? to,
        Guid? departmentId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var claims = await GetClaimsQuery(tenantId, from, to, departmentId, status)
            .Include(c => c.Employee)
                .ThenInclude(e => e.Department)
            .Include(c => c.ClaimType)
            .Include(c => c.Currency)
            .OrderBy(c => c.ReferenceNumber)
            .ToListAsync(cancellationToken);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header()
                    .Text("Claims Management Report")
                    .FontSize(20)
                    .FontColor(Colors.Blue.Medium)
                    .Bold();

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(100);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Ref").Bold();
                                header.Cell().Element(CellStyle).Text("Employee").Bold();
                                header.Cell().Element(CellStyle).Text("Department").Bold();
                                header.Cell().Element(CellStyle).Text("Type").Bold();
                                header.Cell().Element(CellStyle).Text("Status").Bold();
                                header.Cell().Element(CellStyle).Text("Amount").Bold();
                            });

                            foreach (var claim in claims)
                            {
                                table.Cell().Element(CellStyle).Text(claim.ReferenceNumber);
                                table.Cell().Element(CellStyle).Text(claim.Employee?.FullName ?? "");
                                table.Cell().Element(CellStyle).Text(claim.Employee?.Department?.Name ?? "");
                                table.Cell().Element(CellStyle).Text(claim.ClaimType?.Name ?? "");
                                table.Cell().Element(CellStyle).Text(claim.Status.ToString());
                                table.Cell().Element(CellStyle).Text($"{claim.Currency?.Code ?? ""} {claim.TotalAmount:N2}");
                            }
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GeneratePaymentsReportAsync(
        Guid tenantId,
        DateTime? from,
        DateTime? to,
        Guid? batchId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ClaimPayments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Claim)
                .ThenInclude(c => c.Employee)
            .Include(p => p.Claim)
                .ThenInclude(c => c.Currency)
            .Include(p => p.PaymentBatch)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(p => p.PaidAtUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(p => p.PaidAtUtc <= to.Value);
        if (batchId.HasValue)
            query = query.Where(p => p.PaymentBatchId == batchId.Value);

        var payments = await query
            .OrderBy(p => p.PaidAtUtc)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Payments Report");

        // Headers
        worksheet.Cell(1, 1).Value = "Payment Date";
        worksheet.Cell(1, 2).Value = "Claim Reference";
        worksheet.Cell(1, 3).Value = "Employee";
        worksheet.Cell(1, 4).Value = "Amount";
        worksheet.Cell(1, 5).Value = "Currency";
        worksheet.Cell(1, 6).Value = "Payment Reference";
        worksheet.Cell(1, 7).Value = "Batch Reference";

        // Data
        for (int i = 0; i < payments.Count; i++)
        {
            var payment = payments[i];
            var row = i + 2;
            
            worksheet.Cell(row, 1).Value = payment.PaidAtUtc?.ToString("yyyy-MM-dd") ?? "";
            worksheet.Cell(row, 2).Value = payment.Claim?.ReferenceNumber ?? "";
            worksheet.Cell(row, 3).Value = payment.Claim?.Employee?.FullName ?? "";
            worksheet.Cell(row, 4).Value = payment.Amount;
            worksheet.Cell(row, 5).Value = payment.Claim?.Currency?.Code ?? "";
            worksheet.Cell(row, 6).Value = payment.PaymentReference ?? "";
            worksheet.Cell(row, 7).Value = payment.PaymentBatch?.Reference ?? "";
        }

        // Format
        worksheet.Columns().AdjustToContents();
        worksheet.Range(1, 1, 1, 7).Style.Font.Bold = true;
        worksheet.Range(1, 1, payments.Count + 1, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(1, 1, payments.Count + 1, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private IQueryable<Claim> GetClaimsQuery(
        Guid tenantId,
        DateTime? from,
        DateTime? to,
        Guid? departmentId,
        string? status)
    {
        var query = _db.Claims
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(c => c.CreatedAtUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(c => c.CreatedAtUtc <= to.Value);
        if (departmentId.HasValue)
            query = query.Where(c => c.Employee.DepartmentId == departmentId.Value);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ClaimStatus>(status, out var statusEnum))
            query = query.Where(c => c.Status == statusEnum);

        return query;
    }

    static IContainer CellStyle(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(2)
            .AlignCenter()
            .AlignMiddle();
    }
}
