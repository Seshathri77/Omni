# Yath Activity Service

## Overview
Activity Service is the social feed engine for the Yath travel platform, managing posts, likes, comments, and user feeds.

## Features
- **Post Management**: Create, read, update, delete travel posts with photos, tags, and locations
- **Social Interactions**: Like/unlike posts, add/delete comments
- **Feed Generation**: Personalized feeds based on followed users and public posts
- **Trip Integration**: Posts can be associated with trips for trip-specific activity feeds
- **Search & Discovery**: Search posts by tags, browse user profiles

## Architecture
- **Framework**: .NET 8, ASP.NET Core Web API
- **Database**: MongoDB (yath_activity database)
- **Messaging**: OmniFlow with RabbitMQ (or in-memory for dev)
- **Authentication**: JWT Bearer tokens
- **Observability**: Serilog + Seq, OpenTelemetry, Prometheus metrics

## Domain Models

### Post
- **PostId**: Unique identifier
- **UserId**: Author of the post
- **Content**: Post caption/text
- **MediaUrls**: List of photo/video URLs
- **TripId**: Optional associated trip
- **Location**: Optional geo-location (name, lat, lon)
- **Tags**: List of hashtags
- **LikesCount, CommentsCount, SharesCount**: Engagement metrics
- **Visibility**: Public, Followers, Private

### Comment
- **CommentId**: Unique identifier
- **PostId**: Parent post
- **UserId**: Comment author
- **Content**: Comment text
- **ParentCommentId**: Optional (for threaded comments/replies)

### Like
- **PostId**: Liked post
- **UserId**: User who liked
- **LikedAt**: Timestamp

## MongoDB Collections

### posts
```javascript
{
  _id: ObjectId,
  postId: "uuid",
  userId: "user-id",
  content: "Amazing sunset in Bali!",
  mediaUrls: ["url1", "url2"],
  tripId: "trip-id",
  location: {
    name: "Bali, Indonesia",
    latitude: -8.4095,
    longitude: 115.1889
  },
  tags: ["bali", "sunset", "travel"],
  likesCount: 42,
  commentsCount: 5,
  sharesCount: 2,
  visibility: "public",
  createdAt: ISODate,
  updatedAt: ISODate
}
```

**Indexes**:
- `postId` (unique)
- `userId`
- `tripId`
- `tags`
- `createdAt` (descending)

### comments
```javascript
{
  _id: ObjectId,
  commentId: "uuid",
  postId: "post-uuid",
  userId: "user-id",
  content: "Beautiful!",
  parentCommentId: null, // or comment-id for replies
  createdAt: ISODate
}
```

**Indexes**:
- `commentId` (unique)
- `postId`
- `parentCommentId`

### likes
```javascript
{
  _id: ObjectId,
  postId: "post-uuid",
  userId: "user-id",
  likedAt: ISODate
}
```

**Indexes**:
- Compound `(postId, userId)` (unique)
- `postId`

## API Endpoints

### Post Management
- `POST /api/posts` - Create new post
- `GET /api/posts/{postId}` - Get post details
- `PUT /api/posts/{postId}` - Update post
- `DELETE /api/posts/{postId}` - Delete post
- `GET /api/posts/user/{userId}` - Get user's posts
- `GET /api/posts/feed` - Get personalized feed
- `GET /api/posts/trip/{tripId}` - Get trip posts

### Social Interactions
- `POST /api/posts/{postId}/like` - Like post
- `DELETE /api/posts/{postId}/like` - Unlike post
- `POST /api/posts/{postId}/comment` - Add comment
- `GET /api/posts/{postId}/comments` - Get comments
- `DELETE /api/posts/comments/{commentId}` - Delete comment

### Health & Metrics
- `GET /health` - Health check
- `GET /metrics` - Prometheus metrics

## Events Published

### ActivityCreated
```csharp
public record ActivityCreated(
    string ActivityId,
    string UserId,
    string? TripId,
    string Caption,
    LocationInfo? Location,
    List<string> Tags,
    List<string> MediaUrls,
    string Visibility,
    DateTime CreatedAt
) : IEvent;
```

### ActivityUpdated
```csharp
public record ActivityUpdated(
    string ActivityId,
    string? Caption,
    List<string>? Tags,
    DateTime UpdatedAt
) : IEvent;
```

### ActivityDeleted
```csharp
public record ActivityDeleted(
    string ActivityId,
    string UserId,
    DateTime DeletedAt
) : IEvent;
```

### ActivityLiked / ActivityUnliked
```csharp
public record ActivityLiked(
    string ActivityId,
    string LikedBy,
    DateTime LikedAt
) : IEvent;
```

### CommentAdded
```csharp
public record CommentAdded(
    string CommentId,
    string ActivityId,
    string UserId,
    string Text,
    DateTime CreatedAt
) : IEvent;
```

## Events Consumed

### TripCreated
Auto-creates a feed post when a new trip is created:
```csharp
await messageBus.SubscribeAsync<TripCreated>(async (envelope, context) =>
{
    // Auto-create post: "Started planning a trip: [Title]"
    var post = new Post { /* ... */ };
    await postRepository.CreateAsync(post);
});
```

### TripStatusUpdated
Listens to trip status changes for potential feed updates (e.g., "Trip started!", "Trip completed!").

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
cd Yath/Yath.ActivityService/Yath.ActivityService
dotnet restore
dotnet build
dotnet run
```

Service will start on https://localhost:5002 (or configured port)

### Swagger UI
Navigate to https://localhost:5002/swagger to explore and test APIs

### Authentication
All endpoints require JWT authentication:
1. Get a JWT token from User Service (POST /api/users/login)
2. Click "Authorize" in Swagger UI
3. Enter: `Bearer {your-token}`

## Testing with Swagger

1. **Register/Login** via User Service to get JWT token
2. **Authorize** in Activity Service Swagger
3. **Create Post**:
   ```json
   {
     "tripId": "trip-uuid",
     "caption": "Amazing sunset in Bali!",
     "location": {
       "name": "Bali, Indonesia",
       "latitude": -8.4095,
       "longitude": 115.1889,
       "placeId": null
     },
     "tags": ["bali", "sunset", "travel"],
     "mediaIds": ["media-id-1", "media-id-2"],
     "visibility": "public"
   }
   ```
4. **Like Post**: `POST /api/posts/{postId}/like`
5. **Add Comment**:
   ```json
   {
     "text": "Beautiful view!"
   }
   ```

## Dependencies
- OmniFlow.Core
- OmniFlow.Messaging
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
- Post authors reference User.userId
- Client enriches posts with user displayName, avatar

### Trip Service
- Posts can be linked to trips via tripId
- Listens to TripCreated events to auto-post trip milestones
- Listens to TripStatusUpdated for potential feed updates

### Media Service
- Posts reference Media.mediaId for photos/videos
- Client fetches media URLs from Media Service

### Notification Service
- Sends notifications when:
  - Someone likes your post
  - Someone comments on your post
  - Someone you follow creates a post

## Feed Algorithm

Current implementation:
1. Get list of followed users (TODO: fetch from User Service)
2. Fetch posts from followed users with visibility=public
3. Sort by createdAt descending
4. Pagination support (skip, limit)

Future enhancements:
- ML-based personalized feed ranking
- Engagement-based sorting (likes, comments, recency)
- Trip-based feed clustering
- Trending tags and locations

## Performance Considerations
- **Indexes**: Critical indexes on userId, tripId, createdAt for fast queries
- **Counters**: Denormalized likesCount, commentsCount for performance
- **Pagination**: All list endpoints support skip/limit
- **Caching**: Consider Redis for hot feeds (future)

## Future Enhancements
- Search posts by location radius
- Trending hashtags
- Post recommendations based on interests
- Stories/ephemeral posts (24-hour expiry)
- Post scheduling
- Rich media previews
- Post analytics (views, engagement rate)
