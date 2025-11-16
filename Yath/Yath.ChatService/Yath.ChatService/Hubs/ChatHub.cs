using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OmniFlow.Messaging;
using Yath.Shared.Messages;
using Yath.Shared.DTOs;
using Yath.ChatService.Models;
using Yath.ChatService.Repositories;

namespace Yath.ChatService.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChatRoomRepository _roomRepository;
    private readonly IPresenceRepository _presenceRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageRepository messageRepository,
        IChatRoomRepository roomRepository,
        IPresenceRepository presenceRepository,
        IMessageBus messageBus,
        ILogger<ChatHub> logger)
    {
        _messageRepository = messageRepository;
        _roomRepository = roomRepository;
        _presenceRepository = presenceRepository;
        _messageBus = messageBus;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected with ConnectionId {ConnectionId}", 
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // Update all presence records for this user
            var rooms = await _roomRepository.GetByUserIdAsync(userId);
            foreach (var room in rooms)
            {
                await _presenceRepository.UpdateStatusAsync(userId, room.RoomId, PresenceStatus.Offline);
                await _presenceRepository.UpdateConnectionIdAsync(userId, room.RoomId, null);
                
                // Notify room members
                await Clients.Group(room.RoomId).SendAsync("UserOffline", userId);
            }

            _logger.LogInformation("User {UserId} disconnected", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null || !room.ParticipantIds.Contains(userId))
        {
            _logger.LogWarning("User {UserId} attempted to join unauthorized room {RoomId}", userId, roomId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Update presence
        var presence = new UserPresence
        {
            UserId = userId,
            RoomId = roomId,
            Status = PresenceStatus.Online,
            LastSeen = DateTime.UtcNow,
            ConnectionId = Context.ConnectionId
        };
        await _presenceRepository.UpsertAsync(presence);

        // Notify room members
        await Clients.Group(roomId).SendAsync("UserJoined", userId);

        _logger.LogInformation("User {UserId} joined room {RoomId}", userId, roomId);
    }

    public async Task LeaveRoom(string roomId)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        // Update presence
        await _presenceRepository.UpdateStatusAsync(userId, roomId, PresenceStatus.Offline);
        await _presenceRepository.UpdateConnectionIdAsync(userId, roomId, null);

        // Notify room members
        await Clients.Group(roomId).SendAsync("UserLeft", userId);

        _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);
    }

    public async Task SendMessage(string roomId, string? text, string? mediaUrl, LocationInfoDto? location)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null || !room.ParticipantIds.Contains(userId))
        {
            _logger.LogWarning("User {UserId} attempted to send message to unauthorized room {RoomId}", userId, roomId);
            return;
        }

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            RoomId = roomId,
            UserId = userId,
            Text = text,
            MediaUrl = mediaUrl,
            Location = location != null ? new MessageLocation
            {
                Name = location.Name,
                Latitude = location.Latitude,
                Longitude = location.Longitude
            } : null,
            Timestamp = DateTime.UtcNow
        };

        await _messageRepository.CreateAsync(message);

        // Update room's last activity
        room.UpdatedAt = DateTime.UtcNow;
        await _roomRepository.UpdateAsync(room);

        // Publish event
        await _messageBus.PublishAsync(new MessageSent(
            message.MessageId,
            roomId,
            userId,
            text,
            mediaUrl,
            location != null ? new Yath.Shared.Messages.LocationInfo(
                location.Name,
                location.Latitude,
                location.Longitude,
                location.PlaceId
            ) : null,
            DateTime.UtcNow
        ));

        // Broadcast to room
        var messageDto = new MessageDto(
            message.MessageId,
            roomId,
            userId,
            string.Empty, // Username enriched by client
            string.Empty, // DisplayName enriched by client
            null, // AvatarUrl enriched by client
            text,
            mediaUrl,
            location,
            message.ReadBy,
            message.Timestamp
        );

        await Clients.Group(roomId).SendAsync("ReceiveMessage", messageDto);

        _logger.LogInformation("Message {MessageId} sent to room {RoomId}", message.MessageId, roomId);
    }

    public async Task MarkMessageAsRead(string messageId)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        await _messageRepository.MarkAsReadAsync(messageId, userId);

        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message != null)
        {
            await Clients.Group(message.RoomId).SendAsync("MessageRead", messageId, userId);
        }
    }

    public async Task AddReaction(string messageId, string emoji)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        await _messageRepository.AddReactionAsync(messageId, userId, emoji);

        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message != null)
        {
            await Clients.Group(message.RoomId).SendAsync("ReactionAdded", messageId, userId, emoji);
        }

        _logger.LogInformation("User {UserId} added reaction {Emoji} to message {MessageId}", userId, emoji, messageId);
    }

    public async Task RemoveReaction(string messageId, string emoji)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        await _messageRepository.RemoveReactionAsync(messageId, userId, emoji);

        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message != null)
        {
            await Clients.Group(message.RoomId).SendAsync("ReactionRemoved", messageId, userId, emoji);
        }
    }

    public async Task StartTyping(string roomId)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        await Clients.OthersInGroup(roomId).SendAsync("UserTyping", userId);
    }

    public async Task StopTyping(string roomId)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return;

        await Clients.OthersInGroup(roomId).SendAsync("UserStoppedTyping", userId);
    }
}
