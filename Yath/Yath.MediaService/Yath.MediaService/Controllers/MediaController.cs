using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniFlow.Messaging;
using Yath.Shared.DTOs;
using Yath.Shared.Messages;
using Yath.MediaService.Models;
using Yath.MediaService.Repositories;
using Yath.MediaService.Services;

namespace Yath.MediaService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaRepository _mediaRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IImageProcessingService _imageProcessing;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IMediaRepository mediaRepository,
        IBlobStorageService blobStorage,
        IImageProcessingService imageProcessing,
        IMessageBus messageBus,
        ILogger<MediaController> logger)
    {
        _mediaRepository = mediaRepository;
        _blobStorage = blobStorage;
        _imageProcessing = imageProcessing;
        _messageBus = messageBus;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<MediaDto>>> UploadMedia([FromForm] IFormFile file, [FromForm] string? tripId = null, [FromForm] string? caption = null)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest(new ApiResponse<MediaDto>(false, null, "No file uploaded"));

            // Validate file type
            var contentType = file.ContentType.ToLower();
            var mediaType = contentType.StartsWith("image/") ? MediaType.Photo : 
                           contentType.StartsWith("video/") ? MediaType.Video :
                           (MediaType?)null;

            if (!mediaType.HasValue)
                return BadRequest(new ApiResponse<MediaDto>(false, null, "Invalid file type. Only images and videos are supported"));

            // Create media record
            var media = new Media
            {
                MediaId = Guid.NewGuid().ToString(),
                UserId = userId,
                TripId = tripId,
                Type = mediaType.Value,
                FileName = file.FileName,
                ContentType = contentType,
                SizeInBytes = file.Length,
                Caption = caption,
                UploadStatus = UploadStatus.Uploading
            };

            await _mediaRepository.CreateAsync(media);

            // Upload to blob storage
            using var stream = file.OpenReadStream();
            var blobName = await _blobStorage.UploadAsync(stream, file.FileName, contentType);
            media.BlobName = blobName;

            // Process image
            if (mediaType == MediaType.Photo)
            {
                stream.Position = 0;
                
                // Get dimensions
                var (width, height) = await _imageProcessing.GetImageDimensionsAsync(stream);
                media.Width = width;
                media.Height = height;

                // Create thumbnail
                stream.Position = 0;
                var (thumbnailStream, thumbWidth, thumbHeight) = await _imageProcessing.CreateThumbnailAsync(stream);
                
                var thumbnailBlobName = await _blobStorage.UploadAsync(
                    thumbnailStream, 
                    $"thumb_{file.FileName}", 
                    "image/jpeg");
                
                media.ThumbnailBlobName = thumbnailBlobName;
                await thumbnailStream.DisposeAsync();
            }

            // Generate URLs (SAS tokens valid for 1 year)
            media.Url = await _blobStorage.GetSasUrlAsync(blobName, TimeSpan.FromDays(365));
            if (!string.IsNullOrEmpty(media.ThumbnailBlobName))
            {
                media.ThumbnailUrl = await _blobStorage.GetSasUrlAsync(media.ThumbnailBlobName, TimeSpan.FromDays(365));
            }

            media.UploadStatus = UploadStatus.Completed;
            await _mediaRepository.UpdateAsync(media);

            // Publish event
            await _messageBus.PublishAsync(new MediaUploaded(
                media.MediaId,
                media.UserId,
                media.Url,
                media.FileName,
                media.ContentType,
                media.SizeInBytes,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Media {MediaId} uploaded by user {UserId}", media.MediaId, userId);

            var mediaDto = MapToDto(media);
            return Ok(new ApiResponse<MediaDto>(true, mediaDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading media");
            return StatusCode(500, new ApiResponse<MediaDto>(false, null, "Failed to upload media"));
        }
    }

    [HttpGet("{mediaId}")]
    public async Task<ActionResult<ApiResponse<MediaDto>>> GetMedia(string mediaId)
    {
        try
        {
            var media = await _mediaRepository.GetByIdAsync(mediaId);
            if (media == null)
                return NotFound(new ApiResponse<MediaDto>(false, null, "Media not found"));

            var mediaDto = MapToDto(media);
            return Ok(new ApiResponse<MediaDto>(true, mediaDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching media");
            return StatusCode(500, new ApiResponse<MediaDto>(false, null, "Failed to fetch media"));
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<ApiResponse<List<MediaDto>>>> GetUserMedia(string userId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
    {
        try
        {
            var mediaList = await _mediaRepository.GetByUserIdAsync(userId, skip, limit);
            var mediaDtos = mediaList.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<MediaDto>>(true, mediaDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user media");
            return StatusCode(500, new ApiResponse<List<MediaDto>>(false, null, "Failed to fetch media"));
        }
    }

    [HttpGet("trip/{tripId}")]
    public async Task<ActionResult<ApiResponse<List<MediaDto>>>> GetTripMedia(string tripId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
    {
        try
        {
            var mediaList = await _mediaRepository.GetByTripIdAsync(tripId, skip, limit);
            var mediaDtos = mediaList.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<MediaDto>>(true, mediaDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip media");
            return StatusCode(500, new ApiResponse<List<MediaDto>>(false, null, "Failed to fetch media"));
        }
    }

    [HttpDelete("{mediaId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteMedia(string mediaId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var media = await _mediaRepository.GetByIdAsync(mediaId);
            if (media == null)
                return NotFound(new ApiResponse<bool>(false, false, "Media not found"));

            if (media.UserId != userId)
                return Forbid();

            // Delete from blob storage
            await _blobStorage.DeleteAsync(media.BlobName);
            if (!string.IsNullOrEmpty(media.ThumbnailBlobName))
            {
                await _blobStorage.DeleteAsync(media.ThumbnailBlobName);
            }

            // Delete from database
            await _mediaRepository.DeleteAsync(mediaId);

            // Publish event
            await _messageBus.PublishAsync(new MediaDeleted(
                mediaId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Media {MediaId} deleted", mediaId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to delete media"));
        }
    }

    [HttpPut("{mediaId}")]
    public async Task<ActionResult<ApiResponse<MediaDto>>> UpdateMedia(string mediaId, [FromBody] UpdateMediaRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var media = await _mediaRepository.GetByIdAsync(mediaId);
            if (media == null)
                return NotFound(new ApiResponse<MediaDto>(false, null, "Media not found"));

            if (media.UserId != userId)
                return Forbid();

            // Update fields
            if (request.Caption != null)
                media.Caption = request.Caption;
            
            if (request.Tags != null)
                media.Tags = request.Tags;

            await _mediaRepository.UpdateAsync(media);

            _logger.LogInformation("Media {MediaId} updated", mediaId);

            var mediaDto = MapToDto(media);
            return Ok(new ApiResponse<MediaDto>(true, mediaDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating media");
            return StatusCode(500, new ApiResponse<MediaDto>(false, null, "Failed to update media"));
        }
    }

    private MediaDto MapToDto(Media media)
    {
        return new MediaDto(
            media.MediaId,
            media.Url,
            media.ThumbnailUrl,
            media.Type.ToString().ToLower(),
            media.Width,
            media.Height,
            media.CreatedAt
        );
    }
}

public record UpdateMediaRequest(
    string? Caption,
    List<string>? Tags
);
