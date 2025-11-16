using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Yath.MediaService.Services;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(
        string connectionString, 
        string containerName,
        ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType)
    {
        try
        {
            var blobName = $"{Guid.NewGuid()}/{fileName}";
            var blobClient = _containerClient.GetBlobClient(blobName);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            };

            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = blobHttpHeaders
            });

            _logger.LogInformation("Uploaded blob: {BlobName}", blobName);
            return blobName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading blob");
            throw;
        }
    }

    public async Task<Stream> DownloadAsync(string blobName)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            var response = await blobClient.DownloadAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading blob: {BlobName}", blobName);
            throw;
        }
    }

    public async Task DeleteAsync(string blobName)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
            _logger.LogInformation("Deleted blob: {BlobName}", blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting blob: {BlobName}", blobName);
            throw;
        }
    }

    public async Task<string> GetSasUrlAsync(string blobName, TimeSpan validity)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            if (!blobClient.CanGenerateSasUri)
            {
                // Fallback to public URL if SAS not supported
                return blobClient.Uri.ToString();
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerClient.Name,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAS URL for blob: {BlobName}", blobName);
            throw;
        }
    }
}
