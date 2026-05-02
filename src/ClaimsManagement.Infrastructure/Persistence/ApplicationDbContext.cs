using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<ClaimType> ClaimTypes => Set<ClaimType>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimLine> ClaimLines => Set<ClaimLine>();
    public DbSet<ClaimAttachment> ClaimAttachments => Set<ClaimAttachment>();
    public DbSet<ClaimApproval> ClaimApprovals => Set<ClaimApproval>();
    public DbSet<ClaimComment> ClaimComments => Set<ClaimComment>();
    public DbSet<ClaimStatusHistory> ClaimStatusHistories => Set<ClaimStatusHistory>();
    public DbSet<PaymentBatch> PaymentBatches => Set<PaymentBatch>();
    public DbSet<ClaimPayment> ClaimPayments => Set<ClaimPayment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<RateType> RateTypes { get; set; }
    public DbSet<Rate> Rates { get; set; }
    public DbSet<Bank> Banks { get; set; }
    public DbSet<BankBranch> BankBranches { get; set; }
    public DbSet<UserBankAccount> UserBankAccounts { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Slug).HasMaxLength(128);
        });

        modelBuilder.Entity<Department>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code });
            e.Property(x => x.Name).HasMaxLength(256);
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(256);
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasOne(x => x.Department).WithMany(d => d.Employees).HasForeignKey(x => x.DepartmentId);
        });

        modelBuilder.Entity<Currency>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(16);
        });

        modelBuilder.Entity<ExchangeRate>(e =>
        {
            e.HasOne(x => x.FromCurrency).WithMany(c => c.RatesFrom).HasForeignKey(x => x.FromCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ToCurrency).WithMany(c => c.RatesTo).HasForeignKey(x => x.ToCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClaimType>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.FormSchemaJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Claim>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ReferenceNumber }).IsUnique();
            e.Property(x => x.ReferenceNumber).HasMaxLength(64);
            e.Property(x => x.Title).HasMaxLength(512);
            e.Property(x => x.TotalAmount).HasPrecision(18, 4);
            e.Property(x => x.DynamicDataJson).HasColumnType("jsonb");
            e.Property(x => x.BankDetailsJson).HasColumnType("jsonb");
            e.HasOne(x => x.ClaimType).WithMany(t => t.Claims).HasForeignKey(x => x.ClaimTypeId);
            e.HasOne(x => x.Employee).WithMany(em => em.Claims).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId);
        });

        modelBuilder.Entity<ClaimLine>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(1024);
            e.Property(x => x.LineTotal).HasPrecision(18, 4);
            e.Property(x => x.UnitAmount).HasPrecision(18, 4);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
            e.HasOne(x => x.Claim).WithMany(c => c.Lines).HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClaimAttachment>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(512);
            e.Property(x => x.StoredPath).HasMaxLength(2048);
            e.HasOne(x => x.Claim).WithMany(c => c.Attachments).HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClaimApproval>(e =>
        {
            e.HasIndex(x => new { x.ClaimId, x.StepOrder }).IsUnique();
            e.Property(x => x.StepName).HasMaxLength(128);
            e.HasOne(x => x.Claim).WithMany(c => c.Approvals).HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClaimComment>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasOne(x => x.Claim).WithMany(c => c.Comments).HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClaimStatusHistory>(e =>
        {
            e.HasOne(x => x.Claim).WithMany(c => c.StatusHistory).HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentBatch>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(512);
            e.Property(x => x.TotalAmount).HasPrecision(18, 4);
            e.HasIndex(x => new { x.TenantId, x.Reference }).IsUnique();
            e.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId);
        });

        modelBuilder.Entity<ClaimPayment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.Property(x => x.OriginalAmount).HasPrecision(18, 4);
            e.Property(x => x.PaymentReference).HasMaxLength(128);
            e.HasOne(x => x.Claim).WithMany(c => c.Payments).HasForeignKey(x => x.ClaimId);
            e.HasOne(x => x.PaymentBatch).WithMany(b => b.Payments).HasForeignKey(x => x.PaymentBatchId);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.EntityType).HasMaxLength(128);
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.ChangesJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(256);
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasIndex(x => new { x.TenantId, x.UserId });
        });

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(x => x.TenantId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.Property(x => x.TokenHash).HasMaxLength(128);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RateType>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Unit).HasMaxLength(64);
            e.Property(x => x.DefaultAmount).HasPrecision(18, 4);
            e.Property(x => x.MaxDailyAmount).HasPrecision(18, 4);
            e.Property(x => x.Category).HasMaxLength(128);
            e.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId);
        });

        modelBuilder.Entity<Rate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.RateTypeId, x.EmployeeId });
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.Property(x => x.Location).HasMaxLength(256);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.RateType).WithMany(rt => rt.Rates).HasForeignKey(x => x.RateTypeId);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        modelBuilder.Entity<Bank>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Country).HasMaxLength(128);
            e.Property(x => x.Currency).HasMaxLength(16);
            e.Property(x => x.Website).HasMaxLength(512);
            e.Property(x => x.ContactInfo).HasMaxLength(1000);
        });

        modelBuilder.Entity<BankBranch>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.BankId, x.Code });
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Address).HasMaxLength(512);
            e.Property(x => x.City).HasMaxLength(128);
            e.Property(x => x.Country).HasMaxLength(128);
            e.Property(x => x.PhoneNumber).HasMaxLength(64);
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasOne(x => x.Bank).WithMany(b => b.Branches).HasForeignKey(x => x.BankId);
        });

        modelBuilder.Entity<UserBankAccount>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.UserId, x.AccountNumber });
            e.Property(x => x.AccountNumber).HasMaxLength(128);
            e.Property(x => x.AccountName).HasMaxLength(256);
            e.Property(x => x.AccountType).HasMaxLength(64);
            e.Property(x => x.Currency).HasMaxLength(16);
            e.Property(x => x.RoutingNumber).HasMaxLength(64);
            e.Property(x => x.SwiftCode).HasMaxLength(64);
            e.Property(x => x.Iban).HasMaxLength(128);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.Bank).WithMany(b => b.UserAccounts).HasForeignKey(x => x.BankId);
            e.HasOne(x => x.Branch).WithMany(b => b.Accounts).HasForeignKey(x => x.BranchId);
        });

        modelBuilder.Entity<PaymentMethod>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.UserId });
            e.Property(x => x.Type).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Currency).HasMaxLength(16);
            e.Property(x => x.Details).HasMaxLength(1000);
            e.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId);
        });
    }
}
