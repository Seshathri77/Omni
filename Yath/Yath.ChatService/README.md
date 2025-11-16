# Yath Chat Service

Real-time messaging service with SignalR for trip-based chat rooms on the Yath travel platform.

## Features

- **Real-time Messaging**: Instant message delivery via SignalR WebSockets
- **Trip-based Chat Rooms**: Dedicated chat rooms for each trip
- **Presence Tracking**: Online/offline/away status for participants
- **Typing Indicators**: See when others are typing
- **Message Reactions**: Emoji reactions to messages
- **Read Receipts**: Track which messages have been read
- **Message History**: Persistent storage in MongoDB
- **Media Sharing**: Share photos and location in chat
- **Reply Threading**: Reply to specific messages

## Technology Stack

- **.NET 8**: Modern C# web API
- **SignalR**: Real-time bidirectional communication
- **MongoDB**: Document storage for rooms, messages, presence
- **OmniFlow Framework**: Message bus, observability, correlation tracking
- **JWT Authentication**: Secure WebSocket connections
- **Serilog + Seq**: Structured logging

## Architecture

### Domain Models

**ChatRoom**: Trip chat room with participants
```csharp
{
    RoomId: "guid",
    TripId: "trip-id",
    ParticipantIds: ["user1", "user2"],
    CreatedAt: "2024-01-15T10:00:00Z",
    UpdatedAt: "2024-01-15T12:30:00Z"
}
```

**Message**: Chat message with optional media/location
```csharp
{
    MessageId: "guid",
    RoomId: "room-id",
    UserId: "user-id",
    Text: "Hello everyone!",
    MediaUrl: "https://...",
    Location: {
        Name: "Eiffel Tower",
        Latitude: 48.8584,
        Longitude: 2.2945
    },
    ReplyToMessageId: "parent-message-id",
    ReadBy: ["user1", "user2"],
    Reactions: [
        { UserId: "user1", Emoji: "👍", Timestamp: "..." }
    ],
    IsDeleted: false,
    Timestamp: "2024-01-15T10:00:00Z"
}
```

**UserPresence**: User online status per room
```csharp
{
    UserId: "user-id",
    RoomId: "room-id",
    Status: "online" | "away" | "offline",
    LastSeen: "2024-01-15T10:00:00Z",
    ConnectionId: "signalr-connection-id"
}
```

## API Endpoints (REST)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/chat/rooms` | Get user's chat rooms |
| GET | `/api/chat/rooms/{roomId}` | Get room details |
| GET | `/api/chat/rooms/{roomId}/messages` | Get message history |
| GET | `/api/chat/rooms/{roomId}/presence` | Get participant presence |
| DELETE | `/api/chat/messages/{messageId}` | Delete message (soft delete) |

## SignalR Hub Methods

### Client → Server

```typescript
// Connection
connection.start()

// Join room
connection.invoke("JoinRoom", roomId)

// Send message
connection.invoke("SendMessage", roomId, text, mediaUrl, location)

// Mark as read
connection.invoke("MarkMessageAsRead", messageId)

// Reactions
connection.invoke("AddReaction", messageId, emoji)
connection.invoke("RemoveReaction", messageId, emoji)

// Typing indicators
connection.invoke("StartTyping", roomId)
connection.invoke("StopTyping", roomId)

// Leave room
connection.invoke("LeaveRoom", roomId)
```

### Server → Client

```typescript
// Receive messages
connection.on("ReceiveMessage", (message: MessageDto) => {})

// User presence
connection.on("UserJoined", (userId: string) => {})
connection.on("UserLeft", (userId: string) => {})
connection.on("UserOffline", (userId: string) => {})

// Message updates
connection.on("MessageRead", (messageId: string, userId: string) => {})
connection.on("ReactionAdded", (messageId: string, userId: string, emoji: string) => {})
connection.on("ReactionRemoved", (messageId: string, userId: string, emoji: string) => {})

// Typing indicators
connection.on("UserTyping", (userId: string) => {})
connection.on("UserStoppedTyping", (userId: string) => {})
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "yath-api",
    "Audience": "yath-users"
  },
  "Urls": "http://localhost:5005"
}
```

### CORS Configuration

Important for SignalR to work from frontend:

```csharp
policy.WithOrigins("http://localhost:3000") // Your frontend URL
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials(); // Required for SignalR
```

## Event Subscriptions

- **CreateChatRoom**: Creates room when trip is created
- **TripParticipantAdded**: Adds user to room
- **TripParticipantRemoved**: Removes user from room

## Events Published

- **ChatRoomCreated**: New room created for trip
- **MessageSent**: New message sent to room

## Database Schema

### MongoDB Collections

**chat_rooms**
- Index: `roomId` (unique)
- Index: `tripId` (unique)
- Index: `participantIds`

**messages**
- Index: `messageId` (unique)
- Index: `roomId`
- Index: `timestamp` (descending)
- Compound Index: `roomId` + `timestamp`

**user_presence**
- Compound Index: `userId` + `roomId` (unique)
- Index: `roomId`

## Running Locally

```bash
# Start MongoDB
docker run -d -p 27017:27017 --name mongo mongo:latest

# Start Seq (optional)
docker run -d -p 5341:80 --name seq datalust/seq:latest

# Run service
cd Yath/Yath.ChatService/Yath.ChatService
dotnet run
```

Service available at: `http://localhost:5005`

SignalR Hub: `http://localhost:5005/hubs/chat`

Swagger UI: `http://localhost:5005/swagger`

## Client Integration

### JavaScript/TypeScript (SignalR Client)

```bash
npm install @microsoft/signalr
```

```typescript
import * as signalR from "@microsoft/signalr";

// Create connection
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5005/hubs/chat", {
    accessTokenFactory: () => jwtToken
  })
  .withAutomaticReconnect()
  .configureLogging(signalR.LogLevel.Information)
  .build();

// Listen for messages
connection.on("ReceiveMessage", (message) => {
  console.log("New message:", message);
  // Update UI
});

// Listen for typing
connection.on("UserTyping", (userId) => {
  console.log(`${userId} is typing...`);
});

// Start connection
await connection.start();
console.log("SignalR Connected");

// Join room
await connection.invoke("JoinRoom", roomId);

// Send message
await connection.invoke("SendMessage", roomId, "Hello!", null, null);

// Typing indicator
await connection.invoke("StartTyping", roomId);
setTimeout(() => connection.invoke("StopTyping", roomId), 3000);
```

### React Hook Example

```typescript
const useChatRoom = (roomId: string, token: string) => {
  const [connection, setConnection] = useState<signalR.HubConnection>();
  const [messages, setMessages] = useState<Message[]>([]);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/chat`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build();

    conn.on("ReceiveMessage", (message: Message) => {
      setMessages(prev => [message, ...prev]);
    });

    conn.start()
      .then(() => conn.invoke("JoinRoom", roomId))
      .catch(err => console.error(err));

    setConnection(conn);

    return () => {
      conn.invoke("LeaveRoom", roomId);
      conn.stop();
    };
  }, [roomId, token]);

  const sendMessage = async (text: string) => {
    await connection?.invoke("SendMessage", roomId, text, null, null);
  };

  return { messages, sendMessage };
};
```

## Example Usage

### 1. Get User's Chat Rooms (REST)

```bash
GET /api/chat/rooms
Authorization: Bearer {token}
```

Response:
```json
{
  "success": true,
  "data": [
    {
      "roomId": "room-123",
      "tripId": "trip-456",
      "tripName": "Paris Trip",
      "participantIds": ["user1", "user2"],
      "unreadCount": 5,
      "lastMessage": {
        "messageId": "msg-789",
        "text": "See you tomorrow!",
        "timestamp": "2024-01-15T10:00:00Z"
      },
      "createdAt": "2024-01-10T09:00:00Z"
    }
  ]
}
```

### 2. Get Message History (REST)

```bash
GET /api/chat/rooms/room-123/messages?skip=0&limit=50
Authorization: Bearer {token}
```

### 3. Real-time Chat (SignalR)

```typescript
// Connect
await connection.start();

// Join room
await connection.invoke("JoinRoom", "room-123");

// Listen for messages
connection.on("ReceiveMessage", (msg) => {
  appendMessage(msg);
});

// Send message
await connection.invoke("SendMessage", "room-123", "Hello!", null, null);

// Add reaction
await connection.invoke("AddReaction", "msg-789", "👍");

// Typing indicator
await connection.invoke("StartTyping", "room-123");
```

## Observability

- **Structured Logs**: All SignalR events logged with correlation IDs
- **Connection Tracking**: User connections/disconnections logged
- **Health Checks**: `/health` endpoint
- **Metrics**: `/metrics` endpoint (Prometheus format)
- **SignalR Tracing**: Detailed logging for connection lifecycle

## Scalability

### Horizontal Scaling with Redis Backplane

For multiple instances, add Redis backplane:

```bash
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

```csharp
services.AddSignalR()
    .AddStackExchangeRedis("localhost:6379");
```

### Connection Limits

Configure in appsettings.json:

```json
"SignalR": {
  "MaximumReceiveMessageSize": 32768,
  "StreamBufferCapacity": 10
}
```

## Security

### Authentication

- JWT tokens validated for SignalR connections
- Token passed via query string: `?access_token={jwt}`
- Automatic reconnection with token refresh

### Authorization

- Users can only join rooms they're participants in
- Users can only delete their own messages
- Room membership verified on every Hub method

## Testing SignalR Connections

### Using Postman

1. Create WebSocket request to `ws://localhost:5005/hubs/chat`
2. Send handshake: `{"protocol":"json","version":1}`
3. Invoke methods: `{"type":1,"target":"SendMessage","arguments":["room-123","Hello"]}`

### Using Browser Console

```javascript
const conn = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5005/hubs/chat", {
    accessTokenFactory: () => "your-jwt-token"
  })
  .build();

await conn.start();
await conn.invoke("JoinRoom", "room-123");
```

## Future Enhancements

- [ ] Voice messages
- [ ] Video calls (WebRTC integration)
- [ ] Message search (full-text search)
- [ ] Message editing
- [ ] File attachments (docs, PDFs)
- [ ] GIF support via Giphy
- [ ] Polls and surveys
- [ ] Message translation
- [ ] AI-powered smart replies
- [ ] Group video calls
- [ ] Screen sharing

## Troubleshooting

### SignalR Connection Fails

- Check CORS settings (must allow credentials)
- Verify JWT token in query string
- Check firewall/proxy WebSocket support

### Messages Not Received

- Verify user joined room via `JoinRoom`
- Check connection state (`connection.state`)
- Review server logs for errors

### High Memory Usage

- Implement message pagination
- Clean up old presence records
- Add TTL indexes to MongoDB
