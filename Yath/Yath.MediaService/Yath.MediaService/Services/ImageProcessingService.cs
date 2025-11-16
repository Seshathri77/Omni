using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Yath.MediaService.Services;

public interface IImageProcessingService
{
    Task<(Stream thumbnailStream, int width, int height)> CreateThumbnailAsync(Stream imageStream, int maxWidth = 300, int maxHeight = 300);
    Task<(int width, int height)> GetImageDimensionsAsync(Stream imageStream);
}

public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<(Stream thumbnailStream, int width, int height)> CreateThumbnailAsync(
        Stream imageStream, 
        int maxWidth = 300, 
        int maxHeight = 300)
    {
        try
        {
            using var image = await Image.LoadAsync(imageStream);
            
            // Calculate aspect ratio
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);
            
            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            image.Mutate(x => x.Resize(newWidth, newHeight));

            var thumbnailStream = new MemoryStream();
            await image.SaveAsync(thumbnailStream, new JpegEncoder { Quality = 85 });
            thumbnailStream.Position = 0;

            _logger.LogInformation("Created thumbnail: {Width}x{Height}", newWidth, newHeight);
            
            return (thumbnailStream, newWidth, newHeight);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating thumbnail");
            throw;
        }
    }

    public async Task<(int width, int height)> GetImageDimensionsAsync(Stream imageStream)
    {
        try
        {
            var imageInfo = await Image.IdentifyAsync(imageStream);
            return (imageInfo.Width, imageInfo.Height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting image dimensions");
            throw;
        }
    }
}
