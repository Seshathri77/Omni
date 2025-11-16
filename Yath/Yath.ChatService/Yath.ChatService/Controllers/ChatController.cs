using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yath.Shared.DTOs;
using Yath.ChatService.Models;
using Yath.ChatService.Repositories;

namespace Yath.ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatRoomRepository _roomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IPresenceRepository _presenceRepository;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatRoomRepository roomRepository,
        IMessageRepository messageRepository,
        IPresenceRepository presenceRepository,
        ILogger<ChatController> logger)
    {
        _roomRepository = roomRepository;
        _messageRepository = messageRepository;
        _presenceRepository = presenceRepository;
        _logger = logger;
    }

    [HttpGet("rooms")]
    public async Task<ActionResult<ApiResponse<List<ChatRoomDto>>>> GetUserRooms()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var rooms = await _roomRepository.GetByUserIdAsync(userId);
            var roomDtos = new List<ChatRoomDto>();

            foreach (var room in rooms)
            {
                // Get last message
                var messages = await _messageRepository.GetByRoomIdAsync(room.RoomId, 0, 1);
                var lastMessage = messages.FirstOrDefault();

                // Get unread count
                var allMessages = await _messageRepository.GetByRoomIdAsync(room.RoomId, 0, 100);
                var unreadCount = allMessages.Count(m => !m.ReadBy.Contains(userId));

                roomDtos.Add(new ChatRoomDto(
                    room.RoomId,
                    room.TripId,
                    string.Empty, // TripName enriched by client
                    room.ParticipantIds,
                    unreadCount,
                    lastMessage != null ? new MessageDto(
                        lastMessage.MessageId,
                        lastMessage.RoomId,
                        lastMessage.UserId,
                        string.Empty,
                        string.Empty,
                        null,
                        lastMessage.Text,
                        lastMessage.MediaUrl,
                        lastMessage.Location != null ? new LocationInfoDto(
                            lastMessage.Location.Name,
                            lastMessage.Location.Latitude,
                            lastMessage.Location.Longitude,
                            null
                        ) : null,
                        lastMessage.ReadBy,
                        lastMessage.Timestamp
                    ) : null,
                    room.CreatedAt
                ));
            }

            return Ok(new ApiResponse<List<ChatRoomDto>>(true, roomDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user rooms");
            return StatusCode(500, new ApiResponse<List<ChatRoomDto>>(false, null, "Failed to fetch rooms"));
        }
    }

    [HttpGet("rooms/{roomId}")]
    public async Task<ActionResult<ApiResponse<ChatRoomDto>>> GetRoom(string roomId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var room = await _roomRepository.GetByIdAsync(roomId);
            if (room == null || !room.ParticipantIds.Contains(userId))
                return NotFound(new ApiResponse<ChatRoomDto>(false, null, "Room not found"));

            var messages = await _messageRepository.GetByRoomIdAsync(room.RoomId, 0, 1);
            var lastMessage = messages.FirstOrDefault();

            var allMessages = await _messageRepository.GetByRoomIdAsync(room.RoomId, 0, 100);
            var unreadCount = allMessages.Count(m => !m.ReadBy.Contains(userId));

            var roomDto = new ChatRoomDto(
                room.RoomId,
                room.TripId,
                string.Empty,
                room.ParticipantIds,
                unreadCount,
                lastMessage != null ? new MessageDto(
                    lastMessage.MessageId,
                    lastMessage.RoomId,
                    lastMessage.UserId,
                    string.Empty,
                    string.Empty,
                    null,
                    lastMessage.Text,
                    lastMessage.MediaUrl,
                    lastMessage.Location != null ? new LocationInfoDto(
                        lastMessage.Location.Name,
                        lastMessage.Location.Latitude,
                        lastMessage.Location.Longitude,
                        null
                    ) : null,
                    lastMessage.ReadBy,
                    lastMessage.Timestamp
                ) : null,
                room.CreatedAt
            );

            return Ok(new ApiResponse<ChatRoomDto>(true, roomDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching room");
            return StatusCode(500, new ApiResponse<ChatRoomDto>(false, null, "Failed to fetch room"));
        }
    }

    [HttpGet("rooms/{roomId}/messages")]
    public async Task<ActionResult<ApiResponse<List<MessageDto>>>> GetRoomMessages(
        string roomId, 
        [FromQuery] int skip = 0, 
        [FromQuery] int limit = 50)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var room = await _roomRepository.GetByIdAsync(roomId);
            if (room == null || !room.ParticipantIds.Contains(userId))
                return NotFound(new ApiResponse<List<MessageDto>>(false, null, "Room not found"));

            var messages = await _messageRepository.GetByRoomIdAsync(roomId, skip, limit);
            var messageDtos = messages.Select(m => new MessageDto(
                m.MessageId,
                m.RoomId,
                m.UserId,
                string.Empty,
                string.Empty,
                null,
                m.Text,
                m.MediaUrl,
                m.Location != null ? new LocationInfoDto(
                    m.Location.Name,
                    m.Location.Latitude,
                    m.Location.Longitude,
                    null
                ) : null,
                m.ReadBy,
                m.Timestamp
            )).ToList();

            return Ok(new ApiResponse<List<MessageDto>>(true, messageDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching messages");
            return StatusCode(500, new ApiResponse<List<MessageDto>>(false, null, "Failed to fetch messages"));
        }
    }

    [HttpDelete("messages/{messageId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteMessage(string messageId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message == null)
                return NotFound(new ApiResponse<bool>(false, false, "Message not found"));

            if (message.UserId != userId)
                return Forbid();

            await _messageRepository.DeleteAsync(messageId);

            _logger.LogInformation("Message {MessageId} deleted", messageId);

            return Ok(new ApiResponse<bool>(true, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message");
            return StatusCode(500, new ApiResponse<bool>(false, false, "Failed to delete message"));
        }
    }

    [HttpGet("rooms/{roomId}/presence")]
    public async Task<ActionResult<ApiResponse<List<UserPresenceDto>>>> GetRoomPresence(string roomId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var room = await _roomRepository.GetByIdAsync(roomId);
            if (room == null || !room.ParticipantIds.Contains(userId))
                return NotFound(new ApiResponse<List<UserPresenceDto>>(false, null, "Room not found"));

            var presences = await _presenceRepository.GetByRoomIdAsync(roomId);
            var presenceDtos = presences.Select(p => new UserPresenceDto(
                p.UserId,
                p.Status.ToString().ToLower(),
                p.LastSeen
            )).ToList();

            return Ok(new ApiResponse<List<UserPresenceDto>>(true, presenceDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching presence");
            return StatusCode(500, new ApiResponse<List<UserPresenceDto>>(false, null, "Failed to fetch presence"));
        }
    }
}

public record UserPresenceDto(
    string UserId,
    string Status,
    DateTime LastSeen
);
