# Yath Location Service

Real-time GPS tracking service for Yath travel application using SignalR for live location updates, with MongoDB for location history storage.

## Features

- **Real-time Location Tracking**: Live GPS updates using SignalR WebSockets
- **Trip-based Tracking**: Share location with trip participants in real-time
- **Location History**: Complete route tracking with distance, speed, and timing metrics
- **Sharing Modes**: Control who can see your location (Private, Trip Participants, Followers, Public)
- **Distance Calculation**: Haversine formula for accurate distance tracking
- **Battery Monitoring**: Track device battery during location sharing
- **Movement Detection**: Automatic detection of moving vs stationary states
- **Location History Analytics**: Average speed, max speed, total distance per session
- **TTL Management**: Automatic cleanup of old location data (90 days)
- **Geofencing Ready**: Extensible for future geofence notifications

## Technology Stack

- **.NET 8**: Modern web API framework
- **SignalR**: Real-time bidirectional communication
- **MongoDB**: Document database for location data storage
- **JWT Authentication**: Secure access control
- **OmniFlow**: Message-driven architecture framework
- **Serilog + Seq**: Structured logging and monitoring

## Architecture

### Domain Models

#### LocationUpdate
```csharp
public class LocationUpdate
{
    string LocationId
    string UserId
    string? TripId
    double Latitude
    double Longitude
    double Accuracy      // meters
    double? Altitude     // meters
    double? Speed        // m/s
    double? Heading      // degrees
    DateTime Timestamp
    int? BatteryLevel    // percentage
    bool IsMoving
}
```

#### TrackingSession
```csharp
public class TrackingSession
{
    string SessionId
    string UserId
    string? TripId
    DateTime StartedAt
    DateTime? EndedAt
    bool IsActive
    SharingMode SharingMode  // Private, TripParticipants, Followers, Public
    string? ConnectionId
    DateTime LastUpdateAt
    double TotalDistance     // meters
    int LocationCount
}
```

#### LocationHistory
```csharp
public class LocationHistory
{
    string HistoryId
    string UserId
    string TripId
    string SessionId
    List<LocationPoint> Points
    DateTime StartTime
    DateTime EndTime
    double TotalDistance
    double AverageSpeed
    double MaxSpeed
}
```

#### Geofence (Future)
```csharp
public class Geofence
{
    string GeofenceId
    string Name
    string? TripId
    string CreatedBy
    double CenterLatitude
    double CenterLongitude
    double RadiusMeters
    bool IsActive
    bool NotifyOnEnter
    bool NotifyOnExit
}
```

## API Endpoints

### REST API

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/location/session/active` | Get active tracking session | ✅ |
| GET | `/api/location/sessions` | Get user's session history | ✅ |
| GET | `/api/location/trip/{tripId}/history` | Get trip location history | ✅ |
| GET | `/api/location/session/{sessionId}/history` | Get specific session history | ✅ |
| GET | `/api/location/recent?minutes=60` | Get recent location updates | ✅ |
| GET | `/api/location/trip/{tripId}/locations` | Get trip location updates | ✅ |
| DELETE | `/api/location/user/data` | Delete all user location data | ✅ |

### SignalR Hub: `/hubs/location`

#### Client → Server Methods

```typescript
// Start tracking session
connection.invoke("StartTracking", tripId, sharingMode)
  .then(sessionId => console.log("Session started:", sessionId));

// Update location
const location = {
  latitude: 37.7749,
  longitude: -122.4194,
  accuracy: 10.5,
  altitude: 50,
  speed: 5.2,
  heading: 180,
  batteryLevel: 85
};
await connection.invoke("UpdateLocation", location);

// End tracking
await connection.invoke("EndTracking", sessionId);

// Get live locations for trip
const liveLocations = await connection.invoke("GetTripLiveLocations", tripId);

// Join trip tracking group (to receive updates)
await connection.invoke("JoinTripTracking", tripId);

// Leave trip tracking group
await connection.invoke("LeaveTripTracking", tripId);
```

#### Server → Client Events

```typescript
// Receive location update
connection.on("LocationUpdate", (data) => {
  console.log(`User ${data.userId} at ${data.latitude}, ${data.longitude}`);
  console.log(`Speed: ${data.speed} m/s, Moving: ${data.isMoving}`);
  updateMapMarker(data.userId, data.latitude, data.longitude);
});

// User started tracking
connection.on("TrackingStarted", (data) => {
  console.log(`User ${data.userId} started tracking for trip ${data.tripId}`);
});

// User stopped tracking
connection.on("TrackingStopped", (data) => {
  console.log(`User ${data.userId} stopped tracking`);
  console.log(`Total distance: ${data.totalDistance}m`);
  console.log(`Duration: ${data.duration}`);
});
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://localhost:27017"
  },
  "Jwt": {
    "Secret": "your-secret-key",
    "Issuer": "YathAuthService",
    "Audience": "YathUsers"
  },
  "Serilog": {
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "Seq", "Args": { "serverUrl": "http://localhost:5341" } }
    ]
  },
  "Urls": "http://localhost:5006"
}
```

### CORS Configuration

SignalR requires `AllowCredentials()` for WebSocket connections:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});
```

## Event Integration

### Subscribes To
- None currently (extensible for future trip events)

### Publishes
- **LocationUpdated**: When user location changes
  ```csharp
  LocationUpdated(
    SessionId,
    UserId,
    Latitude,
    Longitude,
    Accuracy,
    Timestamp
  )
  ```

## Database Schema

### Collections

#### `location_updates`
```javascript
{
  _id: "location-123",
  userId: "user-456",
  tripId: "trip-789",
  latitude: 37.7749,
  longitude: -122.4194,
  accuracy: 10.5,
  altitude: 50,
  speed: 5.2,
  heading: 180,
  timestamp: ISODate("2024-01-15T10:30:00Z"),
  batteryLevel: 85,
  isMoving: true
}
```

**Indexes:**
- `locationId` (unique)
- `userId`
- `tripId`
- `userId + timestamp` (desc)
- `tripId + timestamp` (desc)
- `timestamp` (TTL: 90 days)

#### `tracking_sessions`
```javascript
{
  _id: "session-123",
  userId: "user-456",
  tripId: "trip-789",
  startedAt: ISODate("2024-01-15T10:00:00Z"),
  endedAt: ISODate("2024-01-15T12:00:00Z"),
  isActive: false,
  sharingMode: "TripParticipants",
  connectionId: "conn-xyz",
  lastUpdateAt: ISODate("2024-01-15T12:00:00Z"),
  totalDistance: 15420.5,
  locationCount: 145
}
```

**Indexes:**
- `sessionId` (unique)
- `userId`
- `tripId`
- `userId + isActive`
- `tripId + isActive`

#### `location_history`
```javascript
{
  _id: "history-123",
  userId: "user-456",
  tripId: "trip-789",
  sessionId: "session-123",
  points: [
    {
      latitude: 37.7749,
      longitude: -122.4194,
      accuracy: 10.5,
      altitude: 50,
      speed: 5.2,
      heading: 180,
      timestamp: ISODate("2024-01-15T10:30:00Z")
    }
  ],
  startTime: ISODate("2024-01-15T10:00:00Z"),
  endTime: ISODate("2024-01-15T12:00:00Z"),
  totalDistance: 15420.5,
  averageSpeed: 2.14,
  maxSpeed: 8.5
}
```

**Indexes:**
- `historyId` (unique)
- `userId`
- `tripId`
- `sessionId` (unique)
- `userId + startTime` (desc)

## Running Locally

### Prerequisites

```bash
# MongoDB
docker run -d -p 27017:27017 --name mongodb mongo:latest

# Seq (optional - for logs)
docker run -d -p 5341:80 --name seq datalust/seq:latest
```

### Start the Service

```bash
cd Yath.LocationService
dotnet restore
dotnet build
dotnet run
```

Service will start on `http://localhost:5006`

## Client Integration

### React + TypeScript Example

```typescript
import * as signalR from "@microsoft/signalr";

// Connect to SignalR hub
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5006/hubs/location", {
    accessTokenFactory: () => localStorage.getItem("jwt")!,
  })
  .withAutomaticReconnect()
  .configureLogging(signalR.LogLevel.Information)
  .build();

// Start connection
await connection.start();
console.log("Connected to Location Hub");

// Start tracking
const sessionId = await connection.invoke("StartTracking", tripId, "TripParticipants");

// Get location from device (browser geolocation API)
navigator.geolocation.watchPosition(
  async (position) => {
    const locationUpdate = {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      accuracy: position.coords.accuracy,
      altitude: position.coords.altitude,
      speed: position.coords.speed,
      heading: position.coords.heading,
      batteryLevel: await getBatteryLevel(), // Battery API
    };

    await connection.invoke("UpdateLocation", locationUpdate);
  },
  (error) => console.error("Geolocation error:", error),
  {
    enableHighAccuracy: true,
    maximumAge: 0,
    timeout: 5000,
  }
);

// Listen for others' locations
connection.on("LocationUpdate", (data) => {
  console.log(`Received location from ${data.userId}`);
  
  // Update map marker
  if (mapMarkers[data.userId]) {
    mapMarkers[data.userId].setLatLng([data.latitude, data.longitude]);
  } else {
    mapMarkers[data.userId] = L.marker([data.latitude, data.longitude])
      .addTo(map)
      .bindPopup(`User: ${data.userId}<br>Speed: ${data.speed?.toFixed(2)} m/s`);
  }
});

// Join trip tracking group
await connection.invoke("JoinTripTracking", tripId);

// Stop tracking when done
await connection.invoke("EndTracking", sessionId);
```

### React Native Example

```typescript
import * as Location from 'expo-location';

// Request location permissions
const { status } = await Location.requestForegroundPermissionsAsync();
if (status !== 'granted') {
  console.error('Location permission denied');
  return;
}

// Start location tracking
await connection.invoke("StartTracking", tripId, "TripParticipants");

// Watch position
const subscription = await Location.watchPositionAsync(
  {
    accuracy: Location.Accuracy.BestForNavigation,
    timeInterval: 5000,  // Update every 5 seconds
    distanceInterval: 10, // Or every 10 meters
  },
  async (location) => {
    const battery = await getBatteryLevelAsync();
    
    await connection.invoke("UpdateLocation", {
      latitude: location.coords.latitude,
      longitude: location.coords.longitude,
      accuracy: location.coords.accuracy,
      altitude: location.coords.altitude,
      speed: location.coords.speed,
      heading: location.coords.heading,
      batteryLevel: Math.round(battery * 100),
    });
  }
);

// Clean up
subscription.remove();
```

## Distance Calculation

Uses **Haversine formula** for accurate distance between GPS coordinates:

```csharp
private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double R = 6371e3; // Earth's radius in meters
    var φ1 = lat1 * Math.PI / 180;
    var φ2 = lat2 * Math.PI / 180;
    var Δφ = (lat2 - lat1) * Math.PI / 180;
    var Δλ = (lon2 - lon1) * Math.PI / 180;

    var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
            Math.Cos(φ1) * Math.Cos(φ2) *
            Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

    return R * c; // Distance in meters
}
```

## Observability

### Health Checks
- **Endpoint**: `http://localhost:5006/health`
- **Checks**: MongoDB connectivity

### Metrics (Prometheus)
- Location updates per second
- Active tracking sessions
- Average location accuracy
- Distance tracked per hour

### Logging (Serilog + Seq)
- Connection lifecycle events
- Location update frequency
- Distance calculations
- Error tracking

## Scalability

### SignalR Scaling Options

For multi-server deployments:

1. **Redis Backplane**
```csharp
services.AddSignalR()
    .AddStackExchangeRedis("localhost:6379");
```

2. **Azure SignalR Service**
```csharp
services.AddSignalR()
    .AddAzureSignalR("connection-string");
```

### MongoDB Optimization

- **Sharding**: Shard by `userId` for horizontal scaling
- **TTL Indexes**: Auto-delete old location data (90 days)
- **Compound Indexes**: Optimized for time-range queries
- **Read Preference**: Use secondaries for historical data queries

## Security

### Authentication
- JWT tokens required for all endpoints
- SignalR accepts token via query string: `?access_token={jwt}`

### Authorization
- Users can only access their own location data
- Trip members can view each other's locations
- Admins can view all location data

### Privacy
- **Sharing Modes**: User controls visibility (Private/TripParticipants/Followers/Public)
- **Data Retention**: Auto-delete after 90 days (GDPR compliance)
- **Opt-out**: DELETE `/api/location/user/data` removes all history

## Testing

### Unit Tests
```bash
dotnet test --filter Category=Unit
```

### Integration Tests
```bash
# Start MongoDB and Seq
docker-compose up -d

# Run integration tests
dotnet test --filter Category=Integration
```

### Manual Testing

#### Test with Postman
1. Get JWT token from User Service
2. Use Postman WebSocket feature to connect:
   ```
   ws://localhost:5006/hubs/location?access_token={your-jwt-token}
   ```
3. Send JSON-RPC messages:
   ```json
   {
     "type": 1,
     "target": "StartTracking",
     "arguments": ["trip-123", "TripParticipants"]
   }
   ```

#### Test with Browser Console
```javascript
const jwt = "your-jwt-token";
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`http://localhost:5006/hubs/location?access_token=${jwt}`)
  .build();

await connection.start();
await connection.invoke("StartTracking", "trip-123", "TripParticipants");
```

## Future Enhancements

1. **Geofencing**: Trigger notifications when entering/exiting areas
2. **Location Sharing Links**: Public URLs to share live location
3. **Route Optimization**: Suggest optimal routes based on history
4. **Location Clustering**: Group nearby users on map
5. **Offline Support**: Queue updates when offline, sync when back online
6. **Location Prediction**: ML to predict next location
7. **Heatmaps**: Visualize popular areas from trip data
8. **Speed Alerts**: Notify if speeding detected
9. **Battery Optimization**: Adaptive update frequency based on battery
10. **Multi-device Support**: Sync tracking across devices

## Troubleshooting

### SignalR Connection Issues

**Problem**: Cannot connect to hub
```
Error: Failed to connect
```

**Solution**: Check CORS and JWT token
```csharp
// Ensure CORS allows credentials
.AllowCredentials()

// Verify JWT in query string
?access_token={valid-jwt}
```

### Location Updates Not Broadcasting

**Problem**: Updates not reaching other clients

**Solution**: Ensure clients joined trip group
```typescript
await connection.invoke("JoinTripTracking", tripId);
```

### MongoDB Connection Issues

**Problem**: `MongoConnectionException`

**Solution**: Check MongoDB is running
```bash
docker ps | grep mongo
# or
mongosh --eval "db.version()"
```

### High Memory Usage

**Problem**: Memory grows over time

**Solution**: Enable TTL index and limit history points
```csharp
// Already configured: 90-day TTL on location_updates
// Consider limiting Points array size in LocationHistory
```

## Contributing

Location Service follows OmniFlow patterns:
1. All events inherit from `IEvent` (OmniFlow.Core)
2. Use `MessageEnvelope<T>` for event publishing
3. Implement idempotency for event handlers
4. Use correlation IDs for distributed tracing

## License

Part of the Yath travel platform. See main repository for license details.
