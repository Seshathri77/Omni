namespace Yath.MediaService.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType);
    Task<Stream> DownloadAsync(string blobName);
    Task DeleteAsync(string blobName);
    Task<string> GetSasUrlAsync(string blobName, TimeSpan validity);
}
