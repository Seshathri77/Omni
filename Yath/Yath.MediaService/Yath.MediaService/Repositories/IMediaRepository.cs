using Yath.MediaService.Models;

namespace Yath.MediaService.Repositories;

public interface IMediaRepository
{
    Task<Media?> GetByIdAsync(string mediaId);
    Task<List<Media>> GetByUserIdAsync(string userId, int skip = 0, int limit = 50);
    Task<List<Media>> GetByTripIdAsync(string tripId, int skip = 0, int limit = 50);
    Task<List<Media>> GetByActivityIdAsync(string activityId);
    Task<Media> CreateAsync(Media media);
    Task UpdateAsync(Media media);
    Task DeleteAsync(string mediaId);
}
