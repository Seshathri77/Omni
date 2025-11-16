# Yath User Service ✅

The User Service handles user authentication, profiles, and social connections (follow/unfollow) for the Yath travel platform.

## Features

- ✅ User Registration with JWT authentication
- ✅ Login with JWT token generation
- ✅ User profile management
- ✅ Follow/unfollow users
- ✅ User search
- ✅ MongoDB persistence
- ✅ OmniFlow message bus integration
- ✅ Distributed tracing and observability
- ✅ Swagger UI for API testing

## Technologies

- .NET 8
- MongoDB (user data, connections)
- JWT Authentication (BCrypt password hashing)
- OmniFlow Framework (messaging, observability)
- RabbitMQ (message bus)
- Serilog + Seq (logging)
- Swagger/OpenAPI

## API Endpoints

### Authentication

```
POST /api/users/register
POST /api/users/login
```

### User Profile

```
GET  /api/users/{userId}
PUT  /api/users/profile (requires auth)
GET  /api/users/search?q={query}
```

### Social

```
POST   /api/users/{userId}/follow (requires auth)
DELETE /api/users/{userId}/unfollow (requires auth)
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "MongoDB": {
    "DatabaseName": "yath_users"
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyForJwtTokenGeneration123!",
    "Issuer": "Yath.UserService",
    "Audience": "YathApp",
    "ExpiryMinutes": "1440"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "UserName": "guest",
    "Password": "guest"
  },
  "Seq": {
    "Url": "http://localhost:5341"
  }
}
```

## Running the Service

### Prerequisites

1. MongoDB running on `localhost:27017`
2. RabbitMQ running on `localhost:5672`
3. (Optional) Seq running on `localhost:5341` for logs

### Start Infrastructure (Docker)

```bash
# From Omni root directory
docker-compose -f docker-compose-observability.yml up -d
```

### Run the Service

```bash
cd Yath/Yath.UserService/Yath.UserService
dotnet run
```

Service will start on `http://localhost:5000` (or configured port)

## Testing with Swagger

1. Navigate to `http://localhost:5000/swagger`
2. Register a new user via `POST /api/users/register`
3. Copy the JWT token from the response
4. Click "Authorize" button and enter: `Bearer {your-token}`
5. Test authenticated endpoints

## MongoDB Collections

### users
```json
{
  "userId": "guid",
  "username": "string (unique)",
  "email": "string (unique)",
  "passwordHash": "string (bcrypt)",
  "profile": {
    "displayName": "string",
    "bio": "string",
    "avatarUrl": "string",
    "location": "string",
    "travelStyles": ["adventure", "luxury", ...]
  },
  "socialGraph": {
    "followersCount": 0,
    "followingCount": 0
  },
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

### user_connections
```json
{
  "followerId": "userId",
  "followingId": "userId",
  "followedAt": "datetime"
}
```

## Events Published

- `UserRegistered` - When a new user signs up
- `WelcomeEmailRequested` - Triggers notification service to send welcome email
- `UserProfileUpdated` - When user updates their profile
- `UserFollowed` - When user follows another user
- `UserUnfollowed` - When user unfollows another user

## Observability

- **Metrics**: Available at `/metrics` (Prometheus format)
- **Traces**: Exported to Jaeger (if configured)
- **Logs**: Structured logs to Console and Seq

## Next Steps

The User Service is complete! Next microservices to build:

1. **Activity Service** - Social feed, posts, likes, comments
2. **Trip Service** - Trip planning, itineraries
3. **Expense Service** - Expense tracking and splitting
4. **Media Service** - Photo/video upload and processing

---

**Status**: ✅ Complete and Ready for Testing!
