# Yath Travel App - Microservices Architecture

## 🎯 Overview

Yath is a social travel platform combining trip planning, expense tracking, location sharing, and social feed capabilities. Built with React Native (mobile) and .NET microservices orchestrated by OmniFlow saga pattern.

## 🏗️ Technology Stack

- **Frontend**: React Native (iOS/Android)
- **Backend**: .NET 8 Microservices
- **Orchestration**: OmniFlow Framework (Saga Pattern)
- **Database**: MongoDB (per-microservice databases)
- **Message Bus**: RabbitMQ (via OmniFlow)
- **Hosting**: Azure Container Apps
- **Observability**: Application Insights, Prometheus, Jaeger

---

## 🎨 Microservices Architecture

### 1. **Users Service** 
**Port**: 5001 | **Database**: `yath_users`

**Responsibilities**:
- User registration, authentication (JWT)
- User profiles (bio, avatar, travel preferences)
- Follow/unfollow relationships
- User search and discovery

**MongoDB Collections**:
- `users` - User profiles and auth credentials
- `user_relationships` - Follow/follower graph
- `user_preferences` - Travel preferences, notification settings

**Key APIs**:
- `POST /api/users/register`
- `POST /api/users/login`
- `GET /api/users/{userId}/profile`
- `POST /api/users/{userId}/follow`

---

### 2. **Trips Service**
**Port**: 5002 | **Database**: `yath_trips`

**Responsibilities**:
- Trip creation and management
- Itinerary planning (destinations, activities, timeline)
- Trip invitations and member management
- Trip status (planned, active, completed)

**MongoDB Collections**:
- `trips` - Trip metadata (name, dates, status, creator)
- `itineraries` - Detailed day-by-day plans
- `trip_members` - Member list with roles (owner, co-planner, traveler)
- `trip_invitations` - Pending invitations

**Key APIs**:
- `POST /api/trips` - Create trip
- `PUT /api/trips/{tripId}/itinerary` - Update itinerary
- `POST /api/trips/{tripId}/members/invite` - Invite members
- `GET /api/trips/{tripId}/details`

**Saga Orchestration**:
- **TripCreationSaga**: Create trip → Send invitations → Notify members
- **MemberInvitationSaga**: Send invitation → Handle accept/reject → Update permissions

---

### 3. **Expenses Service**
**Port**: 5003 | **Database**: `yath_expenses`

**Responsibilities**:
- Expense recording (who paid, amount, category)
- Expense split calculations (equal, custom, percentage)
- Balance calculations per user
- Settlement tracking (who owes whom)

**MongoDB Collections**:
- `expenses` - Individual expense records
- `expense_splits` - Split details per expense
- `settlements` - Payment records between users
- `balances` - Calculated balances per trip/user

**Key APIs**:
- `POST /api/expenses` - Add expense
- `GET /api/expenses/trip/{tripId}/summary` - Get trip expenses
- `POST /api/expenses/{expenseId}/settle` - Record settlement
- `GET /api/expenses/trip/{tripId}/balances` - Get who owes whom

**Saga Orchestration**:
- **ExpenseSplitSaga**: Record expense → Calculate splits → Update balances → Notify users
- **SettlementSaga**: Record payment → Update balances → Notify payer & payee → Mark settled

---

### 4. **Social/Feed Service**
**Port**: 5004 | **Database**: `yath_social`

**Responsibilities**:
- Activity feed (posts from followed users)
- Post creation (text, location, tags)
- Likes, comments, shares
- Feed ranking and pagination

**MongoDB Collections**:
- `posts` - Post metadata (text, location, timestamp, trip reference)
- `likes` - Like records
- `comments` - Comment threads
- `user_feeds` - Personalized feed cache (optional)

**Key APIs**:
- `POST /api/posts` - Create post
- `GET /api/feed/user/{userId}` - Get personalized feed
- `POST /api/posts/{postId}/like` - Like post
- `POST /api/posts/{postId}/comments` - Add comment

**Saga Orchestration**:
- **PostPublishSaga**: Create post → Link media → Update trip timeline → Notify followers → Update feeds

---

### 5. **Media Service**
**Port**: 5005 | **Database**: `yath_media` | **Storage**: Azure Blob Storage

**Responsibilities**:
- Photo/video upload (Azure Blob Storage)
- Image resizing and thumbnails
- Media metadata (EXIF, location)
- CDN URL generation

**MongoDB Collections**:
- `media_files` - File metadata (URL, size, type, upload timestamp)
- `media_associations` - Links to posts/trips/users

**Key APIs**:
- `POST /api/media/upload` - Upload media (multipart)
- `GET /api/media/{mediaId}` - Get media details
- `DELETE /api/media/{mediaId}` - Delete media

---

### 6. **Location Service**
**Port**: 5006 | **Database**: `yath_locations`

**Responsibilities**:
- Real-time location tracking (WebSocket/SignalR)
- Location history for trips
- Geofencing (notify when member reaches destination)
- Map view of group members

**MongoDB Collections**:
- `location_updates` - Real-time location pings (TTL indexed)
- `location_history` - Historical location data per trip
- `geofences` - Trip destination boundaries

**Key APIs**:
- `POST /api/locations/update` - Update user location
- `GET /api/locations/trip/{tripId}/members` - Get all member locations
- `GET /api/locations/trip/{tripId}/history` - Location history

**Technology**: SignalR for real-time WebSocket connections

---

### 7. **Chat Service**
**Port**: 5007 | **Database**: `yath_chat`

**Responsibilities**:
- Group chat per trip
- Real-time messaging (SignalR)
- Message history and pagination
- Unread message counts

**MongoDB Collections**:
- `chat_rooms` - One per trip
- `messages` - Chat messages with sender, timestamp, read receipts
- `message_read_status` - Track which users read which messages

**Key APIs**:
- `POST /api/chat/{tripId}/messages` - Send message
- `GET /api/chat/{tripId}/messages` - Get message history
- `PUT /api/chat/{tripId}/read` - Mark messages as read

**Technology**: SignalR for real-time chat

---

### 8. **Notifications Service**
**Port**: 5008 | **Database**: `yath_notifications`

**Responsibilities**:
- Push notifications (Firebase Cloud Messaging)
- In-app notifications
- Email notifications (SendGrid/SMTP)
- Notification preferences

**MongoDB Collections**:
- `notifications` - Notification history
- `notification_tokens` - FCM device tokens per user
- `notification_preferences` - User notification settings

**Key APIs**:
- `GET /api/notifications/user/{userId}` - Get notifications
- `PUT /api/notifications/{notificationId}/read` - Mark as read
- `POST /api/notifications/tokens` - Register device token

---

## 🔄 Saga Orchestration Workflows

### 1. Trip Creation Saga

```
TripCreationSaga:
1. Create trip record (Trips Service)
2. Create default itinerary (Trips Service)
3. Create chat room (Chat Service)
4. Send invitations to members (Trips Service)
5. Send notifications (Notifications Service)
6. Create initial expense group (Expenses Service)

Compensation:
- Delete trip
- Delete chat room
- Cancel invitations
```

### 2. Post Publishing Saga

```
PostPublishSaga:
1. Create post record (Social Service)
2. Link media files (Media Service)
3. Update trip timeline (Trips Service)
4. Notify followers (Notifications Service)
5. Update feed cache (Social Service)

Compensation:
- Delete post
- Unlink media
- Remove from trip timeline
```

### 3. Expense Split Saga

```
ExpenseSplitSaga:
1. Validate expense data (Expenses Service)
2. Calculate split amounts (Expenses Service)
3. Update user balances (Expenses Service)
4. Record split details (Expenses Service)
5. Notify affected users (Notifications Service)

Compensation:
- Revert balances
- Delete expense record
- Cancel notifications
```

### 4. Member Invitation Saga

```
MemberInvitationSaga:
1. Create invitation record (Trips Service)
2. Send notification (Notifications Service)
3. On Accept:
   - Add to trip members (Trips Service)
   - Grant chat access (Chat Service)
   - Create expense balance (Expenses Service)
4. On Reject:
   - Mark invitation declined (Trips Service)

Compensation:
- Remove from trip
- Revoke chat access
- Delete expense balance
```

---

## 📊 MongoDB Schema Design

### Users Service - `users` Collection

```json
{
  "_id": "user_123",
  "email": "user@example.com",
  "passwordHash": "...",
  "profile": {
    "displayName": "John Doe",
    "avatarUrl": "https://cdn.yath.app/avatars/user_123.jpg",
    "bio": "Travel enthusiast",
    "location": "San Francisco, CA"
  },
  "stats": {
    "followersCount": 250,
    "followingCount": 180,
    "tripsCount": 15,
    "postsCount": 87
  },
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-11-16T08:15:00Z"
}
```

**Indexes**:
- `email` (unique)
- `profile.displayName` (text search)

---

### Trips Service - `trips` Collection

```json
{
  "_id": "trip_456",
  "name": "Euro Trip 2025",
  "description": "Exploring Europe with friends",
  "status": "active", // planned, active, completed
  "creatorId": "user_123",
  "dates": {
    "startDate": "2025-06-01",
    "endDate": "2025-06-15"
  },
  "members": [
    {
      "userId": "user_123",
      "role": "owner",
      "joinedAt": "2025-01-15T10:30:00Z"
    },
    {
      "userId": "user_456",
      "role": "co-planner",
      "joinedAt": "2025-01-16T14:20:00Z"
    }
  ],
  "destinations": [
    {
      "city": "Paris",
      "country": "France",
      "coordinates": { "lat": 48.8566, "lng": 2.3522 },
      "arrivalDate": "2025-06-01",
      "departureDate": "2025-06-05"
    }
  ],
  "coverImageUrl": "https://cdn.yath.app/trips/trip_456_cover.jpg",
  "privacy": "private", // public, private, friends-only
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-11-16T08:15:00Z"
}
```

**Indexes**:
- `creatorId`
- `members.userId`
- `status`
- `dates.startDate`

---

### Expenses Service - `expenses` Collection

```json
{
  "_id": "expense_789",
  "tripId": "trip_456",
  "paidBy": "user_123",
  "amount": 120.50,
  "currency": "EUR",
  "category": "accommodation", // food, transport, accommodation, activities, other
  "description": "Hotel for 2 nights",
  "receiptUrl": "https://cdn.yath.app/receipts/expense_789.jpg",
  "splitType": "equal", // equal, custom, percentage
  "splits": [
    {
      "userId": "user_123",
      "amount": 60.25
    },
    {
      "userId": "user_456",
      "amount": 60.25
    }
  ],
  "createdAt": "2025-06-02T18:30:00Z",
  "updatedAt": "2025-06-02T18:30:00Z"
}
```

**Indexes**:
- `tripId`
- `paidBy`
- `createdAt`

---

### Social Service - `posts` Collection

```json
{
  "_id": "post_101",
  "userId": "user_123",
  "tripId": "trip_456",
  "content": {
    "text": "Amazing view from the Eiffel Tower! 🗼",
    "location": {
      "name": "Eiffel Tower",
      "coordinates": { "lat": 48.8584, "lng": 2.2945 }
    }
  },
  "mediaIds": ["media_201", "media_202"],
  "tags": ["paris", "eiffeltower", "travel"],
  "engagement": {
    "likesCount": 42,
    "commentsCount": 8,
    "sharesCount": 3
  },
  "visibility": "public", // public, followers, private
  "createdAt": "2025-06-02T15:30:00Z",
  "updatedAt": "2025-06-02T18:45:00Z"
}
```

**Indexes**:
- `userId`
- `tripId`
- `createdAt` (descending for feed)
- `tags` (for search)

---

### Chat Service - `messages` Collection

```json
{
  "_id": "msg_301",
  "chatRoomId": "trip_456",
  "senderId": "user_123",
  "messageType": "text", // text, image, location, system
  "content": {
    "text": "Just checked in at the hotel!",
    "mediaUrl": null
  },
  "readBy": ["user_123", "user_456"],
  "createdAt": "2025-06-02T19:15:00Z"
}
```

**Indexes**:
- `chatRoomId` + `createdAt` (compound, for pagination)
- `senderId`

---

## 🔐 Security & Authentication

### JWT Token Flow

1. User logs in via Users Service
2. JWT issued with claims: `userId`, `email`, `roles`
3. API Gateway validates JWT on all requests
4. Services trust validated tokens from gateway

### Authorization Patterns

- **Trip Access**: Verify user is trip member
- **Expense Modification**: Only creator or trip owner
- **Post Deletion**: Only post author
- **Location Sharing**: Only during active trip with consent

---

## 🌐 API Gateway Pattern

Use **Azure API Management** or **Ocelot** as API Gateway:

```
Mobile App
    ↓
API Gateway (JWT Validation, Rate Limiting)
    ↓
┌──────────┬──────────┬──────────┬──────────┐
Users    Trips    Social    Expenses    ...
Service  Service  Service   Service
```

---

## 🚀 Azure Container Apps Deployment

### Container App Configuration

Each microservice deployed as separate Container App:

```yaml
# Example: trips-service
name: yath-trips-service
properties:
  configuration:
    ingress:
      external: false # Only gateway is external
      targetPort: 5002
    dapr:
      enabled: true
      appId: trips-service
  template:
    containers:
      - name: trips-api
        image: yathacr.azurecr.io/trips-service:latest
        resources:
          cpu: 0.5
          memory: 1Gi
    scale:
      minReplicas: 1
      maxReplicas: 10
      rules:
        - name: http-scaling
          http:
            metadata:
              concurrentRequests: 100
```

### Environment Configuration

**Azure Resources**:
- **Azure Container Apps Environment** - Shared across all services
- **Azure Container Registry** - Docker images
- **Azure Service Bus** - RabbitMQ alternative (production)
- **Azure Cosmos DB (MongoDB API)** - Managed MongoDB
- **Azure Blob Storage** - Media files
- **Azure Application Insights** - Observability

**Configuration via Key Vault**:
- MongoDB connection strings (per service)
- JWT signing keys
- Firebase FCM keys
- Blob storage connection strings

---

## 📡 Message Bus Architecture

### OmniFlow + RabbitMQ (Dev) / Azure Service Bus (Prod)

**Message Flow Example**:

```
Trip Created Event
    ↓
RabbitMQ Exchange
    ↓
┌─────────────┬─────────────┬─────────────┐
Chat Service  Expenses      Notifications
(create room) (init balance) (notify members)
```

**Exchange Topology**:
- `trips.events` - Trip-related events
- `expenses.events` - Expense events
- `social.events` - Post events
- `users.events` - User events

---

## 📊 Observability Stack

### Distributed Tracing (Jaeger)

- All services emit OpenTelemetry spans
- Saga operations automatically traced
- View full request flow across services

### Metrics (Prometheus + Grafana)

Key metrics per service:
- Request rate, latency, errors (RED metrics)
- Saga completion rate, compensation rate
- MongoDB query performance
- Message bus throughput

### Logging (Serilog + Seq / App Insights)

Structured logs with:
- `CorrelationId` - Trace requests across services
- `SagaId` - Track saga operations
- `UserId`, `TripId` - Business context

---

## 🔄 Data Consistency Patterns

### Saga State Management

**MongoDB Saga Repository** (`saga_states` collection per service):

```json
{
  "_id": "saga_trip_creation_123",
  "sagaType": "TripCreationSaga",
  "correlationId": "corr_456",
  "version": 3,
  "state": {
    "tripId": "trip_456",
    "chatRoomCreated": true,
    "invitationsSent": true,
    "notificationsSent": false
  },
  "status": "Running",
  "createdAt": "2025-11-16T10:00:00Z",
  "updatedAt": "2025-11-16T10:00:05Z"
}
```

### Idempotency

All message handlers use MongoDB idempotency store:

```csharp
await messageBus.SubscribeAsync<TripCreatedEvent>(async (envelope, ctx) =>
{
    if (!await idempotencyStore.TryRecordAsync(envelope.MessageId, "TripsService"))
        return; // Already processed
    
    await HandleTripCreated(envelope.Message);
});
```

---

## 🎯 React Native App Structure

### App Architecture

```
src/
├── screens/          # Screen components
│   ├── Feed/
│   ├── Trips/
│   ├── Expenses/
│   ├── Profile/
│   └── Chat/
├── components/       # Reusable components
├── services/         # API clients
│   ├── apiClient.ts
│   ├── authService.ts
│   ├── tripsService.ts
│   └── ...
├── navigation/       # React Navigation setup
├── store/           # Redux/Zustand state management
├── hooks/           # Custom React hooks
└── utils/           # Helpers
```

### Key Libraries

- **Navigation**: React Navigation (Stack, Bottom Tabs, Drawer)
- **State**: Zustand or Redux Toolkit
- **API**: Axios with interceptors (JWT injection)
- **Real-time**: SignalR client for chat/location
- **Maps**: React Native Maps
- **Media**: React Native Image Picker, Video
- **Push**: React Native Firebase

---

## 🔧 Development Workflow

### Local Development

1. **Start Infrastructure**:
   ```bash
   docker-compose -f docker-compose-observability.yml up -d
   ```

2. **Start Services**:
   ```bash
   cd samples/UsersService && dotnet run
   cd samples/TripsService && dotnet run
   # ... repeat for all services
   ```

3. **Run React Native App**:
   ```bash
   cd YathMobileApp
   npm install
   npx react-native run-android # or run-ios
   ```

### CI/CD Pipeline (GitHub Actions)

```yaml
name: Deploy Trips Service

on:
  push:
    paths:
      - 'samples/TripsService/**'

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Build Docker image
        run: docker build -t yathacr.azurecr.io/trips-service:${{ github.sha }} .
      - name: Push to ACR
        run: docker push yathacr.azurecr.io/trips-service:${{ github.sha }}
      - name: Deploy to Azure Container Apps
        run: az containerapp update --name trips-service --image yathacr.azurecr.io/trips-service:${{ github.sha }}
```

---

## 📈 Scalability Considerations

### Horizontal Scaling

- **Stateless Services**: Scale any service independently
- **MongoDB Sharding**: Shard by `userId` or `tripId` for large datasets
- **Message Bus**: Azure Service Bus auto-scales
- **CDN**: Azure CDN for media files

### Caching Strategy

- **Redis Cache** (optional): 
  - User profiles (TTL: 5 minutes)
  - Trip summaries (TTL: 1 minute)
  - Feed cache (TTL: 30 seconds)

### Performance Targets

- API Response Time: < 200ms (p95)
- Real-time Messages: < 100ms delivery
- Feed Load: < 500ms for 50 posts
- Location Updates: Every 10 seconds (active trip)

---

## 🛡️ Error Handling & Resilience

### Saga Compensation

All sagas implement compensation logic:
- Automatic retry (3 attempts with exponential backoff)
- Dead letter queue for failed messages
- Manual compensation dashboard (admin tool)

### Circuit Breaker

Use Polly for resilience:
```csharp
services.AddHttpClient("TripsService")
    .AddTransientHttpErrorPolicy(p => 
        p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

---

## 📝 Next Steps

1. **Phase 1 - MVP** (Weeks 1-4):
   - Users Service (auth, profiles)
   - Trips Service (basic trip creation)
   - Social Service (simple feed)
   - Media Service (photo upload)

2. **Phase 2 - Core Features** (Weeks 5-8):
   - Expenses Service (full split logic)
   - Chat Service (real-time messaging)
   - Notifications Service
   - Saga orchestration

3. **Phase 3 - Advanced** (Weeks 9-12):
   - Location Service (real-time tracking)
   - Advanced feed algorithms
   - Analytics dashboard
   - Performance optimization

4. **Phase 4 - Polish** (Weeks 13-16):
   - UI/UX refinement
   - Load testing
   - Security audit
   - App Store submission

---

## 🎓 OmniFlow Integration Benefits

✅ **Saga Orchestration**: Complex workflows (trip creation, expense split) handled reliably  
✅ **Idempotency**: No duplicate expenses or posts  
✅ **Distributed Tracing**: Full visibility into request flows  
✅ **Message-Driven**: Loose coupling between services  
✅ **Automatic Compensation**: Rollback failed operations  
✅ **MongoDB Native**: Perfect fit with MongoDB per-service pattern  

---

This architecture provides a solid foundation for building a scalable, maintainable social travel platform! 🚀
