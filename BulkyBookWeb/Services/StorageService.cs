using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace BulkyBookWeb.Services;

public interface IStorageService
{
    /// <summary>True when Blob Storage is configured; otherwise falls back to local wwwroot.</summary>
    bool UseBlobStorage { get; }

    /// <summary>Saves an uploaded image and returns its URL (blob URL or local relative path).</summary>
    Task<string> SaveImageAsync(IFormFile file, string folder);

    /// <summary>Deletes an image by its URL (blob or local path).</summary>
    Task DeleteAsync(string imageUrl);
}

public class StorageService : IStorageService
{
    private readonly IWebHostEnvironment _webHost;
    private readonly string _connectionString;
    private readonly string _containerName;

    public StorageService(IWebHostEnvironment webHost, IConfiguration configuration)
    {
        _webHost = webHost;
        _connectionString = configuration["Storage:ConnectionString"] ?? "";
        _containerName = configuration["Storage:ContainerName"] ?? "productimages";
    }

    public bool UseBlobStorage => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<string> SaveImageAsync(IFormFile file, string folder)
    {
        var filename = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);

        if (UseBlobStorage)
        {
            var container = new BlobContainerClient(_connectionString, _containerName);
            try
            {
                await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
            }
            catch
            {
                // account-level public access may be off; still create/use the container (private)
                await container.CreateIfNotExistsAsync();
            }

            var blob = container.GetBlobClient($"{folder}/{filename}");
            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

            return blob.Uri.ToString();
        }

        // local fallback (no connection string configured): save under wwwroot/images/products/{folder}
        var relativeDir = Path.Combine("images", "products", folder);
        var dir = Path.Combine(_webHost.WebRootPath, relativeDir);
        Directory.CreateDirectory(dir);

        var localPath = Path.Combine(dir, filename);
        await using (var fs = new FileStream(localPath, FileMode.Create))
        {
            await file.CopyToAsync(fs);
        }

        return "/" + relativeDir.Replace('\\', '/') + "/" + filename;
    }

    public async Task DeleteAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        if (UseBlobStorage && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            var container = new BlobContainerClient(_connectionString, _containerName);
            // URL format: https://<account>.blob.core.windows.net/<container>/<path...>
            var blobName = string.Concat(uri.Segments.Skip(2));
            if (!string.IsNullOrWhiteSpace(blobName))
            {
                await container.GetBlobClient(blobName).DeleteIfExistsAsync();
            }
            return;
        }

        // local fallback
        var localPath = Path.Combine(_webHost.WebRootPath, imageUrl.TrimStart('/').TrimStart('\\'));
        if (System.IO.File.Exists(localPath))
        {
            System.IO.File.Delete(localPath);
        }
    }
}
