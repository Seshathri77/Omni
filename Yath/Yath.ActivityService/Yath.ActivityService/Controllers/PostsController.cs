using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniFlow.Messaging;
using Yath.Shared.DTOs;
using Yath.Shared.Messages;
using Yath.ActivityService.Models;
using Yath.ActivityService.Repositories;

namespace Yath.ActivityService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly IPostRepository _postRepository;
    private readonly ILikeRepository _likeRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        IPostRepository postRepository,
        ILikeRepository likeRepository,
        ICommentRepository commentRepository,
        IMessageBus messageBus,
        ILogger<PostsController> logger)
    {
        _postRepository = postRepository;
        _likeRepository = likeRepository;
        _commentRepository = commentRepository;
        _messageBus = messageBus;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ActivityDto>>> CreatePost([FromBody] CreateActivityRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var post = new Post
            {
                PostId = Guid.NewGuid().ToString(),
                UserId = userId,
                Content = request.Caption,
                MediaUrls = request.MediaIds ?? new List<string>(),
                TripId = request.TripId,
                Location = request.Location != null ? new PostLocation
                {
                    Name = request.Location.Name,
                    Latitude = request.Location.Latitude,
                    Longitude = request.Location.Longitude
                } : null,
                Tags = request.Tags ?? new List<string>(),
                Visibility = Enum.Parse<PostVisibility>(request.Visibility ?? "public", true)
            };

            await _postRepository.CreateAsync(post);

            // Publish event
            await _messageBus.PublishAsync(new ActivityCreated(
                post.PostId,
                post.UserId,
                post.TripId,
                post.Content,
                post.Location != null ? new Yath.Shared.Messages.LocationInfo(
                    post.Location.Name,
                    post.Location.Latitude,
                    post.Location.Longitude,
                    null
                ) : null,
                post.Tags,
                post.MediaUrls,
                post.Visibility.ToString().ToLower(),
                DateTime.UtcNow
            ));

            _logger.LogInformation("Post {PostId} created by user {UserId}", post.PostId, userId);

            var activityDto = MapToDto(post);
            return Ok(new ApiResponse<ActivityDto>(true, activityDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating post");
            return StatusCode(500, new ApiResponse<ActivityDto>(false, null, "Failed to create post"));
        }
    }

    [HttpGet("{postId}")]
    public async Task<ActionResult<ApiResponse<ActivityDto>>> GetPost(string postId)
    {
        try
        {
            var post = await _postRepository.GetByIdAsync(postId);
            if (post == null)
                return NotFound(new ApiResponse<ActivityDto>(false, null, "Post not found"));

            var activityDto = MapToDto(post);
            return Ok(new ApiResponse<ActivityDto>(true, activityDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching post");
            return StatusCode(500, new ApiResponse<ActivityDto>(false, null, "Failed to fetch post"));
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<ApiResponse<List<ActivityDto>>>> GetUserPosts(string userId, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var posts = await _postRepository.GetByUserIdAsync(userId, skip, limit);
            var activityDtos = posts.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<ActivityDto>>(true, activityDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user posts");
            return StatusCode(500, new ApiResponse<List<ActivityDto>>(false, null, "Failed to fetch posts"));
        }
    }

    [HttpGet("feed")]
    public async Task<ActionResult<ApiResponse<List<ActivityDto>>>> GetFeed([FromQuery] int skip = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // TODO: Get list of followed users from User Service
            // For now, just get public posts from all users
            var posts = await _postRepository.GetFeedAsync(new List<string>(), skip, limit);
            var activityDtos = posts.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<ActivityDto>>(true, activityDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching feed");
            return StatusCode(500, new ApiResponse<List<ActivityDto>>(false, null, "Failed to fetch feed"));
        }
    }

    [HttpGet("trip/{tripId}")]
    public async Task<ActionResult<ApiResponse<List<ActivityDto>>>> GetTripPosts(string tripId, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var posts = await _postRepository.GetByTripIdAsync(tripId, skip, limit);
            var activityDtos = posts.Select(MapToDto).ToList();

            return Ok(new ApiResponse<List<ActivityDto>>(true, activityDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip posts");
            return StatusCode(500, new ApiResponse<List<ActivityDto>>(false, null, "Failed to fetch posts"));
        }
    }

    [HttpPut("{postId}")]
    public async Task<ActionResult<ApiResponse<ActivityDto>>> UpdatePost(string postId, [FromBody] UpdateActivityRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var post = await _postRepository.GetByIdAsync(postId);
            if (post == null)
                return NotFound(new ApiResponse<ActivityDto>(false, null, "Post not found"));

            if (post.UserId != userId)
                return Forbid();

            if (!string.IsNullOrEmpty(request.Caption))
                post.Content = request.Caption;

            if (request.Tags != null)
                post.Tags = request.Tags;

            await _postRepository.UpdateAsync(post);

            // Publish event
            await _messageBus.PublishAsync(new ActivityUpdated(
                post.PostId,
                post.Content,
                post.Tags,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Post {PostId} updated", postId);

            var activityDto = MapToDto(post);
            return Ok(new ApiResponse<ActivityDto>(true, activityDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating post");
            return StatusCode(500, new ApiResponse<ActivityDto>(false, null, "Failed to update post"));
        }
    }

    [HttpDelete("{postId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeletePost(string postId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var post = await _postRepository.GetByIdAsync(postId);
            if (post == null)
                return NotFound(new ApiResponse<bool>(false, false, "Post not found"));

            if (post.UserId != userId)
                return Forbid();

            await _postRepository.DeleteAsync(postId);
            await _likeRepository.DeleteByPostIdAsync(postId);
            await _commentRepository.DeleteByPostIdAsync(postId);

            // Publish event
            await _messageBus.PublishAsync(new ActivityDeleted(
                postId,
                userId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("Post {PostId} deleted", postId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting post");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to delete post"));
        }
    }

    [HttpPost("{postId}/like")]
    public async Task<ActionResult<ApiResponse<bool>>> LikePost(string postId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var post = await _postRepository.GetByIdAsync(postId);
            if (post == null)
                return NotFound(new ApiResponse<bool>(false, false, "Post not found"));

            // Check if already liked
            if (await _likeRepository.HasLikedAsync(postId, userId))
                return BadRequest(new ApiResponse<bool>(false, false, "Already liked"));

            var like = new Like
            {
                PostId = postId,
                UserId = userId
            };

            await _likeRepository.CreateAsync(like);
            await _postRepository.IncrementLikesCountAsync(postId);

            // Publish event
            await _messageBus.PublishAsync(new ActivityLiked(
                postId,
                userId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("User {UserId} liked post {PostId}", userId, postId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error liking post");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to like post"));
        }
    }

    [HttpDelete("{postId}/like")]
    public async Task<ActionResult<ApiResponse<bool>>> UnlikePost(string postId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!await _likeRepository.HasLikedAsync(postId, userId))
                return BadRequest(new ApiResponse<bool>(false, false, "Not liked yet"));

            await _likeRepository.DeleteAsync(postId, userId);
            await _postRepository.DecrementLikesCountAsync(postId);

            // Publish event
            await _messageBus.PublishAsync(new ActivityUnliked(
                postId,
                userId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("User {UserId} unliked post {PostId}", userId, postId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unliking post");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to unlike post"));
        }
    }

    [HttpPost("{postId}/comment")]
    public async Task<ActionResult<ApiResponse<CommentDto>>> AddComment(string postId, [FromBody] Yath.Shared.DTOs.AddCommentRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var post = await _postRepository.GetByIdAsync(postId);
            if (post == null)
                return NotFound(new ApiResponse<CommentDto>(false, null, "Post not found"));

            var comment = new Comment
            {
                CommentId = Guid.NewGuid().ToString(),
                PostId = postId,
                UserId = userId,
                Content = request.Text
            };

            await _commentRepository.CreateAsync(comment);
            await _postRepository.IncrementCommentsCountAsync(postId);

            // Publish event
            await _messageBus.PublishAsync(new CommentAdded(
                comment.CommentId,
                postId,
                userId,
                request.Text,
                DateTime.UtcNow
            ));

            _logger.LogInformation("User {UserId} commented on post {PostId}", userId, postId);

            var commentDto = new CommentDto(
                comment.CommentId,
                comment.PostId,
                comment.UserId,
                string.Empty, // Username will be enriched by client
                string.Empty, // DisplayName will be enriched by client
                null, // AvatarUrl will be enriched by client
                comment.Content,
                comment.CreatedAt
            );

            return Ok(new ApiResponse<CommentDto>(true, commentDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment");
            return StatusCode(500, new ApiResponse<CommentDto>(false, null, "Failed to add comment"));
        }
    }

    [HttpGet("{postId}/comments")]
    public async Task<ActionResult<ApiResponse<List<CommentDto>>>> GetComments(string postId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
    {
        try
        {
            var comments = await _commentRepository.GetByPostIdAsync(postId, skip, limit);
            var commentDtos = comments.Select(c => new CommentDto(
                c.CommentId,
                c.PostId,
                c.UserId,
                string.Empty,
                string.Empty,
                null,
                c.Content,
                c.CreatedAt
            )).ToList();

            return Ok(new ApiResponse<List<CommentDto>>(true, commentDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comments");
            return StatusCode(500, new ApiResponse<List<CommentDto>>(false, null, "Failed to fetch comments"));
        }
    }

    [HttpDelete("comments/{commentId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteComment(string commentId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var comment = await _commentRepository.GetByIdAsync(commentId);
            if (comment == null)
                return NotFound(new ApiResponse<bool>(false, false, "Comment not found"));

            if (comment.UserId != userId)
                return Forbid();

            await _commentRepository.DeleteAsync(commentId);
            await _postRepository.DecrementCommentsCountAsync(comment.PostId);

            _logger.LogInformation("Comment {CommentId} deleted", commentId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to delete comment"));
        }
    }

    private ActivityDto MapToDto(Post post)
    {
        return new ActivityDto(
            post.PostId,
            post.UserId,
            string.Empty, // Username will be enriched by client
            string.Empty, // DisplayName will be enriched by client
            null, // AvatarUrl will be enriched by client
            post.TripId,
            null, // TripName will be enriched by client
            post.Content,
            post.Location != null ? new LocationInfoDto(
                post.Location.Name,
                post.Location.Latitude,
                post.Location.Longitude,
                null
            ) : null,
            post.Tags,
            post.MediaUrls.Select(url => new MediaDto(
                url, // Using URL as MediaId for now
                url,
                null, // ThumbnailUrl
                "photo", // Type
                0, // Width
                0, // Height
                post.CreatedAt
            )).ToList(),
            post.LikesCount,
            post.CommentsCount,
            false, // IsLiked - would need to check current user
            post.Visibility.ToString().ToLower(),
            post.CreatedAt
        );
    }
}

public record UpdateActivityRequest(string? Caption, List<string>? Tags);
