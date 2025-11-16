using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Sagas;
using Yath.Shared.DTOs;
using Yath.Shared.Messages;
using Yath.UserService.Models;
using Yath.UserService.Repositories;
using Yath.UserService.Sagas;
using Yath.UserService.Services;

namespace Yath.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserConnectionRepository _connectionRepository;
    private readonly IAuthService _authService;
    private readonly IMessageBus _messageBus;
    private readonly ISagaRepository<UserRegistrationSagaState> _sagaRepository;
    private readonly ITimerService _timerService;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository userRepository,
        IUserConnectionRepository connectionRepository,
        IAuthService authService,
        IMessageBus messageBus,
        ISagaRepository<UserRegistrationSagaState> sagaRepository,
        ITimerService timerService,
        ICorrelationAccessor correlationAccessor,
        ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _connectionRepository = connectionRepository;
        _authService = authService;
        _messageBus = messageBus;
        _sagaRepository = sagaRepository;
        _timerService = timerService;
        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register([FromBody] RegisterUserRequest request)
    {
        try
        {
            // Check if username or email already exists
            var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
            if (existingUser != null)
                return BadRequest(new ApiResponse<LoginResponse>(false, null, "Username already exists"));

            existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest(new ApiResponse<LoginResponse>(false, null, "Email already exists"));

            // Create user
            var user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Username = request.Username,
                Email = request.Email,
                PasswordHash = _authService.HashPassword(request.Password),
                Profile = new UserProfile
                {
                    DisplayName = request.DisplayName
                }
            };

            await _userRepository.CreateAsync(user);

            // Publish user registered event
            await _messageBus.PublishAsync(new UserRegistered(
                user.UserId,
                user.Username,
                user.Email,
                user.Profile.DisplayName,
                DateTime.UtcNow
            ));

            // Request welcome email
            await _messageBus.PublishAsync(new WelcomeEmailRequested(
                user.UserId,
                user.Email,
                user.Profile.DisplayName
            ));

            // Generate JWT token
            var token = _authService.GenerateJwtToken(user);
            var response = _authService.CreateLoginResponse(user, token);

            _logger.LogInformation("User {Username} registered successfully", user.Username);

            return Ok(new ApiResponse<LoginResponse>(true, response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user");
            return StatusCode(500, new ApiResponse<LoginResponse>(false, null, "Registration failed"));
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        try
        {
            // Find user by email or username
            var user = await _userRepository.GetByEmailAsync(request.EmailOrUsername)
                ?? await _userRepository.GetByUsernameAsync(request.EmailOrUsername);

            if (user == null)
                return Unauthorized(new ApiResponse<LoginResponse>(false, null, "Invalid credentials"));

            // Verify password
            if (!_authService.VerifyPassword(request.Password, user.PasswordHash))
                return Unauthorized(new ApiResponse<LoginResponse>(false, null, "Invalid credentials"));

            // Generate JWT token
            var token = _authService.GenerateJwtToken(user);
            var response = _authService.CreateLoginResponse(user, token);

            _logger.LogInformation("User {Username} logged in successfully", user.Username);

            return Ok(new ApiResponse<LoginResponse>(true, response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new ApiResponse<LoginResponse>(false, null, "Login failed"));
        }
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile(string userId)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiResponse<UserProfileDto>(false, null, "User not found"));

            var currentUserId = User.FindFirst("sub")?.Value;
            var isFollowing = false;

            if (!string.IsNullOrEmpty(currentUserId))
            {
                isFollowing = await _connectionRepository.ExistsAsync(currentUserId, userId);
            }

            var profile = new UserProfileDto(
                user.UserId,
                user.Username,
                user.Profile.DisplayName,
                user.Profile.Bio,
                user.Profile.AvatarUrl,
                user.Profile.Location,
                user.Profile.TravelStyles,
                user.SocialGraph.FollowersCount,
                user.SocialGraph.FollowingCount,
                isFollowing,
                user.CreatedAt
            );

            return Ok(new ApiResponse<UserProfileDto>(true, profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user profile");
            return StatusCode(500, new ApiResponse<UserProfileDto>(false, null, "Failed to fetch profile"));
        }
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiResponse<UserDto>(false, null, "User not found"));

            // Update profile fields
            if (!string.IsNullOrEmpty(request.DisplayName))
                user.Profile.DisplayName = request.DisplayName;
            
            if (request.Bio != null)
                user.Profile.Bio = request.Bio;
            
            if (request.Location != null)
                user.Profile.Location = request.Location;
            
            if (request.TravelStyles != null)
                user.Profile.TravelStyles = request.TravelStyles;

            await _userRepository.UpdateAsync(user);

            // Publish event
            await _messageBus.PublishAsync(new UserProfileUpdated(
                user.UserId,
                user.Profile.DisplayName,
                user.Profile.Bio,
                user.Profile.AvatarUrl,
                DateTime.UtcNow
            ));

            var userDto = new UserDto(
                user.UserId,
                user.Username,
                user.Email,
                user.Profile.DisplayName,
                user.Profile.Bio,
                user.Profile.AvatarUrl,
                user.Profile.Location,
                user.Profile.TravelStyles,
                user.SocialGraph.FollowersCount,
                user.SocialGraph.FollowingCount,
                user.CreatedAt
            );

            return Ok(new ApiResponse<UserDto>(true, userDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, new ApiResponse<UserDto>(false, null, "Failed to update profile"));
        }
    }

    [Authorize]
    [HttpPost("{userId}/follow")]
    public async Task<ActionResult<ApiResponse<bool>>> Follow(string userId)
    {
        try
        {
            var currentUserId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            if (currentUserId == userId)
                return BadRequest(new ApiResponse<bool>(false, false, "Cannot follow yourself"));

            // Check if users exist
            if (!await _userRepository.ExistsAsync(userId))
                return NotFound(new ApiResponse<bool>(false, false, "User not found"));

            // Check if already following
            if (await _connectionRepository.ExistsAsync(currentUserId, userId))
                return BadRequest(new ApiResponse<bool>(false, false, "Already following"));

            // Create connection
            var connection = new UserConnection
            {
                FollowerId = currentUserId,
                FollowingId = userId
            };
            await _connectionRepository.CreateAsync(connection);

            // Update counts
            var follower = await _userRepository.GetByIdAsync(currentUserId);
            var following = await _userRepository.GetByIdAsync(userId);

            if (follower != null)
            {
                follower.SocialGraph.FollowingCount++;
                await _userRepository.UpdateAsync(follower);
            }

            if (following != null)
            {
                following.SocialGraph.FollowersCount++;
                await _userRepository.UpdateAsync(following);
            }

            // Publish event
            await _messageBus.PublishAsync(new UserFollowed(
                currentUserId,
                userId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("User {FollowerId} followed user {FollowingId}", currentUserId, userId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error following user");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to follow user"));
        }
    }

    [Authorize]
    [HttpDelete("{userId}/unfollow")]
    public async Task<ActionResult<ApiResponse<bool>>> Unfollow(string userId)
    {
        try
        {
            var currentUserId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // Check if following
            if (!await _connectionRepository.ExistsAsync(currentUserId, userId))
                return BadRequest(new ApiResponse<bool>(false, false, "Not following"));

            // Delete connection
            await _connectionRepository.DeleteAsync(currentUserId, userId);

            // Update counts
            var follower = await _userRepository.GetByIdAsync(currentUserId);
            var following = await _userRepository.GetByIdAsync(userId);

            if (follower != null)
            {
                follower.SocialGraph.FollowingCount = Math.Max(0, follower.SocialGraph.FollowingCount - 1);
                await _userRepository.UpdateAsync(follower);
            }

            if (following != null)
            {
                following.SocialGraph.FollowersCount = Math.Max(0, following.SocialGraph.FollowersCount - 1);
                await _userRepository.UpdateAsync(following);
            }

            // Publish event
            await _messageBus.PublishAsync(new UserUnfollowed(
                currentUserId,
                userId,
                DateTime.UtcNow
            ));

            _logger.LogInformation("User {FollowerId} unfollowed user {FollowingId}", currentUserId, userId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfollowing user");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to unfollow user"));
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> Search([FromQuery] string q, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
    {
        try
        {
            var users = await _userRepository.SearchAsync(q, skip, limit);
            var userDtos = users.Select(u => new UserDto(
                u.UserId,
                u.Username,
                u.Email,
                u.Profile.DisplayName,
                u.Profile.Bio,
                u.Profile.AvatarUrl,
                u.Profile.Location,
                u.Profile.TravelStyles,
                u.SocialGraph.FollowersCount,
                u.SocialGraph.FollowingCount,
                u.CreatedAt
            )).ToList();

            return Ok(new ApiResponse<List<UserDto>>(true, userDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users");
            return StatusCode(500, new ApiResponse<List<UserDto>>(false, null, "Search failed"));
        }
    }
}
