using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ClaimsManagement.Contracts;

public interface IBankingService
{
    Task<IReadOnlyList<Bank>> GetBanksAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Bank> CreateBankAsync(Guid tenantId, CreateBankRequest request, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<BankBranch> CreateBankBranchAsync(Guid tenantId, Guid bankId, CreateBankBranchRequest request, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankBranch>> GetBankBranchesAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default);
    Task<UserBankAccount> CreateUserBankAccountAsync(Guid tenantId, CreateUserBankAccountRequest request, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserBankAccount>> GetUserBankAccountsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<PaymentMethod> CreatePaymentMethodAsync(Guid tenantId, CreatePaymentMethodRequest request, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentMethod>> GetUserPaymentMethodsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<UserBankAccount> SetDefaultBankAccountAsync(Guid tenantId, Guid userId, Guid accountId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<PaymentMethod> SetDefaultPaymentMethodAsync(Guid tenantId, Guid userId, Guid methodId, Guid updatedByUserId, CancellationToken cancellationToken = default);
}

public class BankingService : IBankingService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public BankingService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<Bank>> GetBanksAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Banks
            .AsNoTracking()
            .Include(b => b.Branches)
            .Where(b => b.TenantId == tenantId && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Bank> CreateBankAsync(Guid tenantId, CreateBankRequest request, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Check for duplicate code
        var existing = await _db.Banks
            .AnyAsync(b => b.TenantId == tenantId && b.Code == request.Code, cancellationToken);
        if (existing)
            throw new InvalidOperationException("A bank with this code already exists.");

        var bank = new Bank
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Code = request.Code,
            Country = request.Country,
            Currency = request.Currency,
            IsActive = true,
            Website = request.Website,
            ContactInfo = request.ContactInfo,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Banks.Add(bank);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(Bank),
            EntityId = bank.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.Name,
                request.Code,
                request.Country,
                request.Currency,
                request.Website,
                request.ContactInfo
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return bank;
    }

    public async Task<BankBranch> CreateBankBranchAsync(Guid tenantId, Guid bankId, CreateBankBranchRequest request, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate bank exists
        var bank = await _db.Banks
            .FirstOrDefaultAsync(b => b.Id == bankId && b.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Bank not found.");

        // Check for duplicate branch code
        var existing = await _db.BankBranches
            .AnyAsync(bb => bb.TenantId == tenantId && bb.BankId == bankId && bb.Code == request.Code, cancellationToken);
        if (existing)
            throw new InvalidOperationException("A branch with this code already exists for this bank.");

        var branch = new BankBranch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BankId = bankId,
            Name = request.Name,
            Code = request.Code,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.BankBranches.Add(branch);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(BankBranch),
            EntityId = branch.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                bankId,
                request.Name,
                request.Code,
                request.Address,
                request.City,
                request.Country,
                request.PhoneNumber,
                request.Email
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return branch;
    }

    public async Task<IReadOnlyList<BankBranch>> GetBankBranchesAsync(Guid tenantId, Guid bankId, CancellationToken cancellationToken = default)
    {
        return await _db.BankBranches
            .AsNoTracking()
            .Where(bb => bb.TenantId == tenantId && bb.BankId == bankId && bb.IsActive)
            .OrderBy(bb => bb.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserBankAccount> CreateUserBankAccountAsync(Guid tenantId, CreateUserBankAccountRequest request, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate bank exists
        var bank = await _db.Banks
            .FirstOrDefaultAsync(b => b.Id == request.BankId && b.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Bank not found.");

        // Validate branch if provided
        if (request.BranchId.HasValue)
        {
            var branch = await _db.BankBranches
                .FirstOrDefaultAsync(bb => bb.Id == request.BranchId.Value && bb.TenantId == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Bank branch not found.");
        }

        // Check for duplicate account number for this user
        var existing = await _db.UserBankAccounts
            .AnyAsync(uba => uba.TenantId == tenantId && uba.UserId == createdByUserId && uba.AccountNumber == request.AccountNumber, cancellationToken);
        if (existing)
            throw new InvalidOperationException("An account with this number already exists for this user.");

        // If this is the first account, make it default
        var existingAccounts = await _db.UserBankAccounts
            .CountAsync(uba => uba.TenantId == tenantId && uba.UserId == createdByUserId, cancellationToken);
        var isDefault = existingAccounts == 0;

        var account = new UserBankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = createdByUserId,
            BankId = request.BankId,
            BranchId = request.BranchId,
            AccountNumber = request.AccountNumber,
            AccountName = request.AccountName,
            AccountType = request.AccountType,
            Currency = request.Currency,
            IsDefault = isDefault,
            IsActive = true,
            RoutingNumber = request.RoutingNumber,
            SwiftCode = request.SwiftCode,
            Iban = request.Iban,
            Notes = request.Notes,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.UserBankAccounts.Add(account);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(UserBankAccount),
            EntityId = account.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.BankId,
                request.BranchId,
                AccountNumber = request.AccountNumber, // Don't log full account number for security
                request.AccountName,
                request.AccountType,
                request.Currency,
                request.RoutingNumber,
                request.SwiftCode,
                HasIban = !string.IsNullOrEmpty(request.Iban)
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(account).Reference(uba => uba.Bank).LoadAsync(cancellationToken);
        await _db.Entry(account).Reference(uba => uba.Branch).LoadAsync(cancellationToken);
        return account;
    }

    public async Task<IReadOnlyList<UserBankAccount>> GetUserBankAccountsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserBankAccounts
            .AsNoTracking()
            .Include(uba => uba.Bank)
            .Include(uba => uba.Branch)
            .Where(uba => uba.TenantId == tenantId && uba.UserId == userId && uba.IsActive)
            .OrderByDescending(uba => uba.IsDefault)
            .ThenBy(uba => uba.AccountName)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentMethod> CreatePaymentMethodAsync(Guid tenantId, CreatePaymentMethodRequest request, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate bank account if provided
        if (request.BankAccountId.HasValue)
        {
            var bankAccount = await _db.UserBankAccounts
                .FirstOrDefaultAsync(uba => uba.Id == request.BankAccountId.Value && uba.TenantId == tenantId && uba.UserId == createdByUserId, cancellationToken)
                ?? throw new InvalidOperationException("Bank account not found or does not belong to this user.");
        }

        // If this is the first payment method, make it default
        var existingMethods = await _db.PaymentMethods
            .CountAsync(pm => pm.TenantId == tenantId && pm.UserId == createdByUserId, cancellationToken);
        var isDefault = existingMethods == 0;

        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = createdByUserId,
            Type = request.Type,
            Name = request.Name,
            BankAccountId = request.BankAccountId?.ToString(),
            Currency = request.Currency,
            IsDefault = isDefault,
            IsActive = true,
            Details = request.Details,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.PaymentMethods.Add(paymentMethod);

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(PaymentMethod),
            EntityId = paymentMethod.Id,
            Action = "Create",
            UserId = createdByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.Type,
                request.Name,
                request.Currency,
                HasBankAccount = request.BankAccountId.HasValue,
                request.Details
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(paymentMethod).Reference(pm => pm.BankAccount).LoadAsync(cancellationToken);
        return paymentMethod;
    }

    public async Task<IReadOnlyList<PaymentMethod>> GetUserPaymentMethodsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.PaymentMethods
            .AsNoTracking()
            .Include(pm => pm.BankAccount)
                .ThenInclude(uba => uba.Bank)
            .Where(pm => pm.TenantId == tenantId && pm.UserId == userId && pm.IsActive)
            .OrderByDescending(pm => pm.IsDefault)
            .ThenBy(pm => pm.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserBankAccount> SetDefaultBankAccountAsync(Guid tenantId, Guid userId, Guid accountId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate account exists and belongs to user
        var account = await _db.UserBankAccounts
            .FirstOrDefaultAsync(uba => uba.Id == accountId && uba.TenantId == tenantId && uba.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Bank account not found or does not belong to this user.");

        // Remove default from all other accounts
        var otherAccounts = await _db.UserBankAccounts
            .Where(uba => uba.TenantId == tenantId && uba.UserId == userId && uba.Id != accountId && uba.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var otherAccount in otherAccounts)
        {
            otherAccount.IsDefault = false;
            otherAccount.ModifiedAtUtc = DateTimeOffset.UtcNow;
        }

        // Set this account as default
        account.IsDefault = true;
        account.ModifiedAtUtc = DateTimeOffset.UtcNow;

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(UserBankAccount),
            EntityId = account.Id,
            Action = "SetDefault",
            UserId = updatedByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                AccountId = accountId,
                AccountName = account.AccountName
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(account).Reference(uba => uba.Bank).LoadAsync(cancellationToken);
        await _db.Entry(account).Reference(uba => uba.Branch).LoadAsync(cancellationToken);
        return account;
    }

    public async Task<PaymentMethod> SetDefaultPaymentMethodAsync(Guid tenantId, Guid userId, Guid methodId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Validate method exists and belongs to user
        var method = await _db.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == methodId && pm.TenantId == tenantId && pm.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Payment method not found or does not belong to this user.");

        // Remove default from all other methods
        var otherMethods = await _db.PaymentMethods
            .Where(pm => pm.TenantId == tenantId && pm.UserId == userId && pm.Id != methodId && pm.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var otherMethod in otherMethods)
        {
            otherMethod.IsDefault = false;
            otherMethod.ModifiedAtUtc = DateTimeOffset.UtcNow;
        }

        // Set this method as default
        method.IsDefault = true;
        method.ModifiedAtUtc = DateTimeOffset.UtcNow;

        // Add audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = nameof(PaymentMethod),
            EntityId = method.Id,
            Action = "SetDefault",
            UserId = updatedByUserId,
            ChangesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                MethodId = methodId,
                MethodName = method.Name,
                MethodType = method.Type
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Load navigation properties
        await _db.Entry(method).Reference(pm => pm.BankAccount).LoadAsync(cancellationToken);
        return method;
    }
}
