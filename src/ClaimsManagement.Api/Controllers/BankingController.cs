using System.Security.Claims;
using ClaimsManagement.Contracts;
using ClaimsManagement.Infrastructure.Identity;
using ClaimsManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[EnableRateLimiting("Authenticated")]
public sealed class BankingController : ControllerBase
{
    private readonly IBankingService _bankingService;
    private readonly UserManager<ApplicationUser> _users;

    public BankingController(IBankingService bankingService, UserManager<ApplicationUser> users)
    {
        _bankingService = bankingService;
        _users = users;
    }

    #region Banks

    [HttpGet("banks")]
    public async Task<ActionResult<IReadOnlyList<BankResponse>>> GetBanks(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var banks = await _bankingService.GetBanksAsync(user.TenantId.Value, cancellationToken);
        var response = banks.Select(b => new BankResponse(
            b.Id,
            b.Code,
            b.Name,
            b.Country,
            b.Currency,
            b.Website,
            b.ContactInfo)).ToList();

        return Ok(response);
    }

    [HttpPost("banks")]
    public async Task<ActionResult<BankResponse>> CreateBank([FromBody] CreateBankRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var bank = await _bankingService.CreateBankAsync(
                user.TenantId.Value,
                new CreateBankRequest(
                    request.Name,
                    request.Code,
                    request.Country,
                    request.Currency,
                    request.Website,
                    request.ContactInfo),
                userId.Value,
                cancellationToken);

            return Created($"/api/banking/banks/{bank.Id}", new BankResponse(
                bank.Id,
                bank.Code,
                bank.Name,
                bank.Country,
                bank.Currency,
                bank.Website,
                bank.ContactInfo));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("banks/{bankId:guid}/branches")]
    public async Task<ActionResult<BankBranchResponse>> CreateBankBranch(Guid bankId, [FromBody] CreateBankBranchRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var branch = await _bankingService.CreateBankBranchAsync(
                user.TenantId.Value,
                bankId,
                new CreateBankBranchRequest(
                    request.Name,
                    request.Code,
                    request.Address,
                    request.City,
                    request.Country,
                    request.PhoneNumber,
                    request.Email),
                userId.Value,
                cancellationToken);

            return Created($"/api/banking/banks/{bankId}/branches/{branch.Id}", new BankBranchResponse(
                branch.Id,
                branch.Code,
                branch.Name,
                branch.Address,
                branch.City,
                branch.Country,
                branch.PhoneNumber,
                branch.Email));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("banks/{bankId:guid}/branches")]
    public async Task<ActionResult<IReadOnlyList<BankBranchResponse>>> GetBankBranches(Guid bankId, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var branches = await _bankingService.GetBankBranchesAsync(user.TenantId.Value, bankId, cancellationToken);
        var response = branches.Select(b => new BankBranchResponse(
            b.Id,
            b.Code,
            b.Name,
            b.Address,
            b.City,
            b.Country,
            b.PhoneNumber,
            b.Email)).ToList();

        return Ok(response);
    }

    #endregion

    #region User Bank Accounts

    [HttpGet("accounts")]
    public async Task<ActionResult<IReadOnlyList<UserBankAccountResponse>>> GetUserBankAccounts(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var accounts = await _bankingService.GetUserBankAccountsAsync(user.TenantId.Value, userId.Value, cancellationToken);
        var response = accounts.Select(uba => new UserBankAccountResponse(
            uba.Id,
            uba.Bank.Id,
            uba.Bank.Name,
            uba.Branch?.Id,
            uba.Branch?.Name,
            uba.AccountNumber,
            uba.AccountName,
            uba.AccountType,
            uba.Currency,
            uba.IsDefault,
            uba.IsActive,
            uba.RoutingNumber,
            uba.SwiftCode,
            uba.Iban,
            uba.Notes)).ToList();

        return Ok(response);
    }

    [HttpPost("accounts")]
    public async Task<ActionResult<UserBankAccountResponse>> CreateUserBankAccount([FromBody] CreateUserBankAccountRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var account = await _bankingService.CreateUserBankAccountAsync(
                user.TenantId.Value,
                new CreateUserBankAccountRequest(
                    request.BankId,
                    request.BranchId,
                    request.AccountNumber,
                    request.AccountName,
                    request.AccountType,
                    request.Currency,
                    request.RoutingNumber,
                    request.SwiftCode,
                    request.Iban,
                    request.Notes),
                userId.Value,
                cancellationToken);

            return Created($"/api/banking/accounts/{account.Id}", new UserBankAccountResponse(
                account.Id,
                account.Bank.Id,
                account.Bank.Name,
                account.Branch?.Id,
                account.Branch?.Name,
                account.AccountNumber,
                account.AccountName,
                account.AccountType,
                account.Currency,
                account.IsDefault,
                account.IsActive,
                account.RoutingNumber,
                account.SwiftCode,
                account.Iban,
                account.Notes));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("accounts/{accountId:guid}/set-default")]
    public async Task<ActionResult<UserBankAccountResponse>> SetDefaultBankAccount(Guid accountId, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var account = await _bankingService.SetDefaultBankAccountAsync(user.TenantId.Value, userId.Value, accountId, userId.Value, cancellationToken);
            return Ok(new UserBankAccountResponse(
                account.Id,
                account.Bank.Id,
                account.Bank.Name,
                account.Branch?.Id,
                account.Branch?.Name,
                account.AccountNumber,
                account.AccountName,
                account.AccountType,
                account.Currency,
                account.IsDefault,
                account.IsActive,
                account.RoutingNumber,
                account.SwiftCode,
                account.Iban,
                account.Notes));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Payment Methods

    [HttpGet("payment-methods")]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodResponse>>> GetUserPaymentMethods(CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        var methods = await _bankingService.GetUserPaymentMethodsAsync(user.TenantId.Value, userId.Value, cancellationToken);
        var response = methods.Select(pm => new PaymentMethodResponse(
            pm.Id,
            pm.Type,
            pm.Name,
            pm.Currency,
            pm.IsDefault,
            pm.IsActive,
            pm.Details,
            pm.BankAccountId != null ? Guid.Parse(pm.BankAccountId) : null,
            pm.BankAccount?.Bank.Name,
            pm.BankAccount?.AccountName)).ToList();

        return Ok(response);
    }

    [HttpPost("payment-methods")]
    public async Task<ActionResult<PaymentMethodResponse>> CreatePaymentMethod([FromBody] CreatePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var method = await _bankingService.CreatePaymentMethodAsync(
                user.TenantId.Value,
                new CreatePaymentMethodRequest(
                    request.Type,
                    request.Name,
                    request.Currency,
                    request.BankAccountId,
                    request.Details),
                userId.Value,
                cancellationToken);

            return Created($"/api/banking/payment-methods/{method.Id}", new PaymentMethodResponse(
                method.Id,
                method.Type,
                method.Name,
                method.Currency,
                method.IsDefault,
                method.IsActive,
                method.Details,
                method.BankAccountId != null ? Guid.Parse(method.BankAccountId) : null,
                method.BankAccount?.Bank.Name,
                method.BankAccount?.AccountName));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("payment-methods/{methodId:guid}/set-default")]
    public async Task<ActionResult<PaymentMethodResponse>> SetDefaultPaymentMethod(Guid methodId, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return BadRequest("User is not associated with a tenant.");

        try
        {
            var method = await _bankingService.SetDefaultPaymentMethodAsync(user.TenantId.Value, userId.Value, methodId, userId.Value, cancellationToken);
            return Ok(new PaymentMethodResponse(
                method.Id,
                method.Type,
                method.Name,
                method.Currency,
                method.IsDefault,
                method.IsActive,
                method.Details,
                method.BankAccountId != null ? Guid.Parse(method.BankAccountId) : null,
                method.BankAccount?.Bank.Name,
                method.BankAccount?.AccountName));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    private Guid? UserGuid()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
