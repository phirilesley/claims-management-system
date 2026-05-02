using System.Security.Claims;
using ClaimsManagement.Api.Contracts;
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
[EnableRateLimiting("FileUpload")]
public sealed class FileUploadController : ControllerBase
{
    private readonly IFileStorageService _fileStorage;
    private readonly UserManager<ApplicationUser> _users;

    public FileUploadController(IFileStorageService fileStorage, UserManager<ApplicationUser> users)
    {
        _fileStorage = fileStorage;
        _users = users;
    }

    [HttpPost]
    public async Task<ActionResult<FileUploadResponse>> Upload([FromForm] FileUploadRequest request, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        if (request.File == null || request.File.Length == 0)
            return BadRequest("No file provided.");

        // Validate file size (10MB max)
        const long maxFileSize = 10 * 1024 * 1024;
        if (request.File.Length > maxFileSize)
            return BadRequest("File size exceeds 10MB limit.");

        // Validate file type
        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".xlsx", ".xls" };
        var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
            return BadRequest("File type not allowed.");

        try
        {
            var storedPath = await _fileStorage.StoreFileAsync(
                user.TenantId.Value,
                request.File.FileName,
                request.File.OpenReadStream(),
                cancellationToken);

            return Ok(new FileUploadResponse(
                request.File.FileName,
                storedPath,
                request.File.Length,
                request.File.ContentType));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error uploading file: {ex.Message}");
        }
    }

    [HttpGet("{storedPath}")]
    public async Task<IActionResult> Download(string storedPath, CancellationToken cancellationToken)
    {
        var userId = UserGuid();
        if (userId == null)
            return Unauthorized();

        var user = await _users.FindByIdAsync(userId.Value.ToString());
        if (user?.TenantId == null)
            return Unauthorized();

        var fileStream = await _fileStorage.GetFileAsync(user.TenantId.Value, storedPath, cancellationToken);
        if (fileStream == null)
            return NotFound();

        // Extract filename from stored path
        var fileName = Path.GetFileName(storedPath);
        var contentType = GetContentType(fileName);

        return File(fileStream, contentType, fileName);
    }

    private Guid? UserGuid()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/octet-stream"
        };
    }
}

public record FileUploadRequest(IFormFile File);

public record FileUploadResponse(
    string OriginalFileName,
    string StoredPath,
    long FileSize,
    string ContentType);
