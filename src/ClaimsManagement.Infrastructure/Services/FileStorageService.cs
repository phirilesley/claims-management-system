using ClaimsManagement.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace ClaimsManagement.Infrastructure.Services;

public interface IFileStorageService
{
    Task<string> StoreFileAsync(Guid tenantId, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<Stream?> GetFileAsync(Guid tenantId, string storedPath, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(Guid tenantId, string storedPath, CancellationToken cancellationToken = default);
    Task<string> GenerateUniquePathAsync(Guid tenantId, string fileName, CancellationToken cancellationToken = default);
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _baseStoragePath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _baseStoragePath = configuration["FileStorage:LocalPath"] ?? "storage";
        
        // Ensure base directory exists
        Directory.CreateDirectory(_baseStoragePath);
    }

    public async Task<string> StoreFileAsync(Guid tenantId, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var uniquePath = await GenerateUniquePathAsync(tenantId, fileName, cancellationToken);
        var fullPath = Path.Combine(_baseStoragePath, uniquePath);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);
        
        return uniquePath;
    }

    public async Task<Stream?> GetFileAsync(Guid tenantId, string storedPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_baseStoragePath, storedPath);
        
        if (!File.Exists(fullPath))
            return null;

        // Security check - ensure file is in expected tenant directory
        var expectedTenantPrefix = Path.Combine(tenantId.ToString());
        if (!storedPath.StartsWith(expectedTenantPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public Task<bool> DeleteFileAsync(Guid tenantId, string storedPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_baseStoragePath, storedPath);
        
        // Security check
        var expectedTenantPrefix = Path.Combine(tenantId.ToString());
        if (!storedPath.StartsWith(expectedTenantPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<string> GenerateUniquePathAsync(Guid tenantId, string fileName, CancellationToken cancellationToken = default)
    {
        var tenantFolder = Path.Combine(tenantId.ToString());
        var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
        
        // Sanitize filename
        var safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        var extension = Path.GetExtension(safeFileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(safeFileName);
        
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var uniqueFileName = $"{nameWithoutExtension}_{uniqueId}{extension}";
        
        var relativePath = Path.Combine(tenantFolder, dateFolder, uniqueFileName);
        return Task.FromResult(relativePath.Replace('\\', '/'));
    }
}

// Future cloud storage implementation (placeholder)
public class CloudFileStorageService : IFileStorageService
{
    public Task<string> StoreFileAsync(Guid tenantId, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Cloud storage not implemented yet");
    }

    public Task<Stream?> GetFileAsync(Guid tenantId, string storedPath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Cloud storage not implemented yet");
    }

    public Task<bool> DeleteFileAsync(Guid tenantId, string storedPath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Cloud storage not implemented yet");
    }

    public Task<string> GenerateUniquePathAsync(Guid tenantId, string fileName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Cloud storage not implemented yet");
    }
}
