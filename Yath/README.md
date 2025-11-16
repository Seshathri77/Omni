# Yath - Travel Companion Platform

A complete microservices-based travel planning and social platform built with .NET 8, MongoDB, and the OmniFlow framework.

## 🏗️ Architecture

Yath consists of 8 microservices:

1. **User Service** (5000) - Authentication, profiles, social connections
2. **Trip Service** (5001) - Trip planning, itineraries, participants
3. **Activity Service** (5002) - Social feed, posts, likes, comments
4. **Expense Service** (5003) - Expense tracking, splitting, settlements
5. **Media Service** (5004) - Photo/video uploads, Azure Blob Storage
6. **Chat Service** (5005) - Real-time messaging with SignalR
7. **Location Service** (5006) - GPS tracking, location sharing with SignalR
8. **Notification Service** (5007) - Push notifications via Firebase FCM

### Technology Stack

- **.NET 8** - All microservices
- **MongoDB** - Database for all services
- **OmniFlow Framework** - Saga orchestration, messaging, observability
- **SignalR** - Real-time communication (Chat, Location)
- **Azure Blob Storage** - Media file storage (Azurite for local dev)
- **Firebase FCM** - Push notifications
- **RabbitMQ** - Message bus for inter-service communication
- **Serilog + Seq** - Structured logging
- **JWT** - Authentication across all services

## 🚀 Quick Start

### Prerequisites

- Docker Desktop
- .NET 8 SDK (for local development)
- Visual Studio 2022 or VS Code

### Running with Docker Compose

1. **Navigate to Yath directory:**
   ```powershell
   cd Yath
   ```

2. **Start all services:**
   ```powershell
   docker-compose up -d
   ```

3. **Check service health:**
   ```powershell
   docker-compose ps
   ```

4. **View logs:**
   ```powershell
   docker-compose logs -f [service-name]
   ```

5. **Stop all services:**
   ```powershell
   docker-compose down
   ```

### Access Points

| Service | URL | Description |
|---------|-----|-------------|
| User Service | http://localhost:5000 | Swagger UI |
| Trip Service | http://localhost:5001 | Swagger UI |
| Activity Service | http://localhost:5002 | Swagger UI |
| Expense Service | http://localhost:5003 | Swagger UI |
| Media Service | http://localhost:5004 | Swagger UI |
| Chat Service | http://localhost:5005 | Swagger UI |
| Location Service | http://localhost:5006 | Swagger UI |
| Notification Service | http://localhost:5007 | Swagger UI |
| MongoDB | localhost:27017 | Username: admin, Password: admin123 |
| RabbitMQ Management | http://localhost:15672 | Username: guest, Password: guest |
| Seq Logs | http://localhost:5341 | Password: Admin123! |
| Azurite Blob | http://localhost:10000 | Azure Storage Emulator |

## 📋 Running Locally (Development)

### 1. Start Infrastructure

```powershell
# Start MongoDB
docker run -d -p 27017:27017 --name yath-mongo -e MONGO_INITDB_ROOT_USERNAME=admin -e MONGO_INITDB_ROOT_PASSWORD=admin123 mongo:7.0

# Start Seq
docker run -d -p 5341:80 --name yath-seq -e ACCEPT_EULA=Y -e SEQ_FIRSTRUN_ADMINPASSWORD=Admin123! datalust/seq

# Start Azurite
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name yath-azurite mcr.microsoft.com/azure-storage/azurite

# Start RabbitMQ (optional)
docker run -d -p 5672:5672 -p 15672:15672 --name yath-rabbitmq rabbitmq:3.12-management
```

### 2. Update Connection Strings

For local development, update appsettings.json in each service:

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://admin:admin123@localhost:27017"
  }
}
```

### 3. Run Services

**Option A: Visual Studio**
- Open `OmniFlow.sln`
- Set multiple startup projects (all Yath services)
- Press F5

**Option B: Command Line**

Open 8 separate terminals:

```powershell
# Terminal 1 - User Service
cd Yath.UserService\Yath.UserService
dotnet run

# Terminal 2 - Trip Service
cd Yath.TripService\Yath.TripService
dotnet run

# ... repeat for all 8 services
```

## 🔐 Authentication

All services use JWT Bearer authentication with unified configuration:

- **Issuer:** YathAuthService
- **Audience:** YathUsers
- **Secret:** Configured in appsettings.json (use environment variables in production)

### Getting Started

1. **Register a user:**
   ```bash
   curl -X POST http://localhost:5000/api/users/register \
     -H "Content-Type: application/json" \
     -d '{
       "username": "johndoe",
       "email": "john@example.com",
       "password": "Password123!",
       "displayName": "John Doe"
     }'
   ```

2. **Login to get token:**
   ```bash
   curl -X POST http://localhost:5000/api/users/login \
     -H "Content-Type: application/json" \
     -d '{
       "emailOrUsername": "johndoe",
       "password": "Password123!"
     }'
   ```

3. **Use token in requests:**
   ```bash
   curl -X GET http://localhost:5001/api/trips/my-trips \
     -H "Authorization: Bearer {your-jwt-token}"
   ```

## 📡 Real-Time Communication

### Chat Service (SignalR)

Connect to: `ws://localhost:5005/hubs/chat?access_token={jwt-token}`

**Methods:**
- `JoinRoom(roomId)` - Join chat room
- `SendMessage(roomId, text, mediaUrl, location)` - Send message
- `MarkAsRead(messageId)` - Mark as read

### Location Service (SignalR)

Connect to: `ws://localhost:5006/hubs/location?access_token={jwt-token}`

**Methods:**
- `StartTracking(tripId, sharingMode)` - Start GPS tracking
- `UpdateLocation(lat, lng, accuracy, altitude, speed, heading)` - Update location
- `SubscribeToTrip(tripId)` - Get participant locations

## 🔧 Configuration

### Environment Variables (Production)

```bash
# JWT Configuration
Jwt__Secret={your-secure-secret-minimum-32-characters}
Jwt__Issuer=YathAuthService
Jwt__Audience=YathUsers

# MongoDB
ConnectionStrings__MongoDb=mongodb://{user}:{password}@{host}:27017

# Azure Storage (Media Service)
ConnectionStrings__AzureStorage={your-azure-connection-string}

# Firebase (Notification Service)
Firebase__CredentialsPath=/app/firebase-adminsdk.json

# Logging
Serilog__WriteTo__1__Args__serverUrl=http://seq:80
```

## 📊 Monitoring & Observability

- **Structured Logs:** All services log to Seq (http://localhost:5341)
- **Health Checks:** Each service exposes `/health` endpoint
- **Metrics:** Available at `/metrics` endpoint (Prometheus format)

## 🧪 Testing

```powershell
# Run all tests
dotnet test

# Run specific service tests
cd tests/OmniFlow.Tests
dotnet test
```

## 📦 Database Collections

Each service has its own MongoDB database:

- **yath_users:** Users, Profiles, Connections
- **yath_trips:** Trips, Itineraries, Participants
- **yath_activities:** Posts, Likes, Comments
- **yath_expenses:** Expenses, Groups, Settlements
- **yath_media:** Media metadata
- **yath_chat:** ChatRooms, Messages, Presence
- **yath_location:** TrackingSessions, LocationUpdates, History
- **yath_notifications:** Notifications, DeviceTokens, Preferences

## 🔄 Event Flow Examples

### Creating a Trip
1. User creates trip → **Trip Service**
2. TripCreated event published
3. **Chat Service** subscribes → Creates chat room
4. ChatRoomCreated event published
5. All participants notified

### Adding an Expense
1. User adds expense → **Expense Service**
2. ExpenseAdded event published
3. **Notification Service** subscribes → Sends push notifications to participants
4. Balances updated in expense group

## 🛠️ Development Tips

1. **Use Swagger:** Each service has interactive API documentation
2. **Check Seq Logs:** View correlated logs across all services
3. **MongoDB Compass:** Connect to view database collections
4. **RabbitMQ Management:** Monitor message queues
5. **SignalR Testing:** Use browser console or Postman WebSocket client

## 📝 API Documentation

See [API_ENDPOINTS.md](API_ENDPOINTS.md) for complete API reference.

## 🤝 Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/amazing-feature`
3. Commit changes: `git commit -m 'Add amazing feature'`
4. Push to branch: `git push origin feature/amazing-feature`
5. Open Pull Request

## 📄 License

This project is part of the OmniFlow framework demonstration.

## 🆘 Troubleshooting

### Port Already in Use
```powershell
# Find process using port
netstat -ano | findstr :5000

# Kill process
taskkill /PID {pid} /F
```

### MongoDB Connection Issues
```powershell
# Check MongoDB is running
docker ps | findstr mongo

# View MongoDB logs
docker logs yath-mongo
```

### Docker Build Issues
```powershell
# Clean and rebuild
docker-compose down -v
docker-compose build --no-cache
docker-compose up -d
```

## 📞 Support

For issues or questions, please open an issue in the repository.
