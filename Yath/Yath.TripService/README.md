# Yath Trip Service

## Overview
Trip Service handles trip planning, itineraries, and trip member management for the Yath social travel platform.

## Features
- **Trip Management**: Create, update, and manage trips with destinations, dates, and descriptions
- **Participant Management**: Add/remove trip members with role-based permissions (Owner, Editor, Viewer)
- **Itinerary Planning**: Day-by-day activity planning with locations, times, and booking info
- **Trip Status Tracking**: Planning → Ongoing → Completed workflow
- **Visibility Control**: Public or private trip visibility
- **Event Publishing**: Publishes trip events for cross-service coordination

## Architecture
- **Framework**: .NET 8, ASP.NET Core Web API
- **Database**: MongoDB (yath_trips database)
- **Messaging**: OmniFlow with RabbitMQ (or in-memory for dev)
- **Authentication**: JWT Bearer tokens
- **Observability**: Serilog + Seq, OpenTelemetry, Prometheus metrics

## Domain Models

### Trip
- **TripId**: Unique identifier
- **CreatorId**: User who created the trip
- **Title & Description**: Trip details
- **Dates**: Start and end dates
- **Destinations**: List of destination names
- **Participants**: List with roles (Owner, Editor, Viewer)
- **Status**: Planning, Ongoing, Completed, Cancelled
- **Visibility**: Public or Private
- **CoverImageUrl**: Optional trip image

### Itinerary
- **ItineraryId**: Unique identifier
- **TripId**: Associated trip
- **Day**: Day number in trip
- **Date**: Specific date
- **Activities**: List of scheduled activities with time, location, type, notes

### Participant Roles
- **Owner**: Full control (creator)
- **Editor**: Can edit trip details and itinerary
- **Viewer**: Read-only access

## MongoDB Collections

### trips
```javascript
{
  _id: ObjectId,
  tripId: "uuid",
  creatorId: "user-id",
  title: "Euro Trip 2024",
  description: "...",
  dates: {
    startDate: ISODate,
    endDate: ISODate
  },
  destinations: ["Paris", "Rome", "Barcelona"],
  participants: [
    { userId: "...", role: "owner", joinedAt: ISODate }
  ],
  status: "planning",
  visibility: "private",
  coverImageUrl: "...",
  createdAt: ISODate,
  updatedAt: ISODate
}
```

**Indexes**:
- `tripId` (unique)
- `creatorId`
- `status`

### itineraries
```javascript
{
  _id: ObjectId,
  itineraryId: "uuid",
  tripId: "trip-uuid",
  day: 1,
  date: ISODate,
  activities: [
    {
      time: "09:00",
      title: "Visit Eiffel Tower",
      location: { name: "...", latitude: 48.858, longitude: 2.294 },
      type: "sightseeing",
      notes: "...",
      bookingInfo: "..."
    }
  ],
  createdAt: ISODate,
  updatedAt: ISODate
}
```

**Indexes**:
- `tripId`
- Compound: `(tripId, day)` (unique)

## API Endpoints

### Trip Management
- `POST /api/trips` - Create new trip
- `GET /api/trips/{tripId}` - Get trip details
- `PUT /api/trips/{tripId}` - Update trip
- `GET /api/trips/my-trips` - Get current user's trips
- `PATCH /api/trips/{tripId}/status` - Update trip status

### Participant Management
- `POST /api/trips/{tripId}/participants` - Add participant
- `DELETE /api/trips/{tripId}/participants/{participantId}` - Remove participant

### Itinerary Management
- `POST /api/trips/{tripId}/itinerary` - Add itinerary day
- `GET /api/trips/{tripId}/itinerary` - Get full itinerary

### Health & Metrics
- `GET /health` - Health check
- `GET /metrics` - Prometheus metrics

## Events Published

### TripCreated
```csharp
public record TripCreated(
    string TripId,
    string CreatorId,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    List<string> Destinations,
    DateTime CreatedAt
) : IEvent;
```

### TripUpdated
```csharp
public record TripUpdated(
    string TripId,
    DateTime UpdatedAt
) : IEvent;
```

### TripParticipantAdded
```csharp
public record TripParticipantAdded(
    string TripId,
    string UserId,
    string Role,
    DateTime AddedAt
) : IEvent;
```

### TripParticipantRemoved
```csharp
public record TripParticipantRemoved(
    string TripId,
    string UserId,
    DateTime RemovedAt
) : IEvent;
```

### TripStatusUpdated
```csharp
public record TripStatusUpdated(
    string TripId,
    string OldStatus,
    string NewStatus,
    DateTime UpdatedAt
) : IEvent;
```

### ItineraryDayAdded
```csharp
public record ItineraryDayAdded(
    string TripId,
    int Day,
    DateTime Date,
    DateTime AddedAt
) : IEvent;
```

### Commands Published

### CreateChatRoom
When a trip is created, requests chat service to create a room for participants.

## Sagas

### TripCreationSaga
Orchestrates trip creation workflow:
1. Trip created (done before saga)
2. Request chat room creation → Chat Service
3. Initialize expense group → Expense Service
4. Send notifications to participants → Notification Service

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "JwtSettings": {
    "SecretKey": "...",
    "Issuer": "Yath",
    "Audience": "YathApp"
  },
  "MessageBus": {
    "Provider": "InMemory" // or "RabbitMQ"
  }
}
```

## Running the Service

### Prerequisites
- .NET 8 SDK
- MongoDB running on localhost:27017
- RabbitMQ (optional, for messaging)
- Seq (optional, for logs)

### Local Development
```bash
cd Yath/Yath.TripService/Yath.TripService
dotnet restore
dotnet build
dotnet run
```

Service will start on https://localhost:5001 (or configured port)

### Swagger UI
Navigate to https://localhost:5001/swagger to explore and test APIs

### Authentication
All endpoints (except /health and /metrics) require JWT authentication:
1. Get a JWT token from User Service (POST /api/users/login)
2. Click "Authorize" in Swagger UI
3. Enter: `Bearer {your-token}`

## Testing with Swagger

1. **Register/Login** via User Service to get JWT token
2. **Authorize** in Trip Service Swagger with Bearer token
3. **Create Trip**:
   ```json
   {
     "title": "Euro Trip 2024",
     "description": "Summer vacation across Europe",
     "startDate": "2024-07-01",
     "endDate": "2024-07-15",
     "destinations": ["Paris", "Rome", "Barcelona"],
     "visibility": "private"
   }
   ```
4. **Add Itinerary**:
   ```json
   {
     "day": 1,
     "date": "2024-07-01",
     "activities": [
       {
         "time": "09:00",
         "title": "Visit Eiffel Tower",
         "location": {
           "name": "Eiffel Tower",
           "latitude": 48.858,
           "longitude": 2.294
         },
         "type": "sightseeing",
         "notes": "Book tickets online",
         "bookingInfo": "https://toureiffel.paris"
       }
     ]
   }
   ```

## Dependencies
- OmniFlow.Core
- OmniFlow.Messaging
- OmniFlow.Sagas
- OmniFlow.Adapters.MongoDb
- OmniFlow.Adapters.RabbitMQ
- OmniFlow.Observability
- OmniFlow.Idempotency
- Yath.Shared
- MongoDB.Driver
- Microsoft.AspNetCore.Authentication.JwtBearer
- Serilog

## Integration with Other Services

### User Service
- Validates JWT tokens
- Trip participants reference User.userId

### Chat Service
- Receives CreateChatRoom commands when trips are created
- Creates chat rooms for trip participants

### Expense Service
- Listens to TripCreated events
- Initializes expense groups for trips with multiple participants

### Notification Service
- Receives SendNotification commands when users are added to trips
- Sends push notifications about trip invitations

### Activity Service
- Listens to TripCreated, TripStatusUpdated events
- Publishes trip milestones to social feed

## Future Enhancements
- Trip templates for popular destinations
- AI-powered itinerary suggestions
- Real-time collaborative editing
- Trip budget tracking
- Integration with booking platforms
- Photo albums per trip
- Trip reviews and ratings
