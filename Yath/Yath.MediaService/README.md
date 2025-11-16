# Yath Media Service

Photo and video upload service with Azure Blob Storage integration and automatic thumbnail generation for the Yath travel platform.

## Features

- **Multi-format Upload**: Support for images (JPEG, PNG, GIF) and videos (MP4, MOV, etc.)
- **Azure Blob Storage**: Scalable cloud storage with SAS token URLs
- **Automatic Thumbnails**: Generated for all images using ImageSharp
- **Image Processing**: Dimension extraction and optimization
- **Trip Association**: Link media to trips for organized galleries
- **Secure Access**: JWT authentication and user-owned media verification

## Technology Stack

- **.NET 8**: Modern C# web API
- **MongoDB**: Document storage for media metadata
- **Azure Blob Storage**: Cloud object storage with SAS URLs
- **SixLabors.ImageSharp**: High-performance image processing
- **OmniFlow Framework**: Message bus, observability, correlation tracking
- **JWT Authentication**: Secure API endpoints
- **Serilog + Seq**: Structured logging

## Architecture

### Domain Model

**Media**: Media file metadata and URLs
```csharp
{
    MediaId: "guid",
    UserId: "user-id",
    TripId: "trip-id",
    ActivityId: "activity-id",
    Type: "photo" | "video",
    Url: "https://storage.blob.core.windows.net/...",
    ThumbnailUrl: "https://storage.blob.core.windows.net/...",
    BlobName: "guid/filename.jpg",
    FileName: "sunset.jpg",
    ContentType: "image/jpeg",
    SizeInBytes: 2048576,
    Width: 1920,
    Height: 1080,
    Duration: null, // For videos (seconds)
    Caption: "Beautiful sunset",
    Tags: ["sunset", "beach"],
    UploadStatus: "completed",
    CreatedAt: "2024-01-15T10:00:00Z"
}
```

### API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/media/upload` | Upload photo/video with optional caption |
| GET | `/api/media/{mediaId}` | Get media metadata |
| GET | `/api/media/user/{userId}` | List user's media |
| GET | `/api/media/trip/{tripId}` | List trip's media gallery |
| PUT | `/api/media/{mediaId}` | Update caption and tags |
| DELETE | `/api/media/{mediaId}` | Delete media and blobs |

### Events Published

- **MediaUploaded**: New media successfully uploaded
- **MediaDeleted**: Media removed from storage

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017",
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=..."
  },
  "BlobStorage": {
    "ContainerName": "yath-media"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "yath-api",
    "Audience": "yath-users"
  },
  "Urls": "http://localhost:5004"
}
```

### Azure Storage Setup

**Development (Azurite Emulator)**:
```json
"AzureStorage": "UseDevelopmentStorage=true"
```

**Production (Azure Blob Storage)**:
```json
"AzureStorage": "DefaultEndpointsProtocol=https;AccountName=yathmedia;AccountKey=...;EndpointSuffix=core.windows.net"
```

## Database Schema

### MongoDB Collections

**media**
- Index: `mediaId` (unique)
- Index: `userId`
- Index: `tripId`
- Index: `activityId`
- Index: `createdAt` (descending)

## Running Locally

### Prerequisites

```bash
# Install Azurite (Azure Storage Emulator)
npm install -g azurite

# OR use Docker
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

### Start Services

```bash
# Start MongoDB
docker run -d -p 27017:27017 --name mongo mongo:latest

# Start Azurite
azurite-blob --location c:\azurite --debug c:\azurite\debug.log

# Start Seq (optional)
docker run -d -p 5341:80 --name seq datalust/seq:latest

# Run service
cd Yath/Yath.MediaService/Yath.MediaService
dotnet run
```

Service available at: `http://localhost:5004`

Swagger UI: `http://localhost:5004/swagger`

## Example Usage

### 1. Upload Photo

```bash
POST /api/media/upload
Authorization: Bearer {token}
Content-Type: multipart/form-data

Form Data:
- file: [sunset.jpg]
- tripId: trip-123
- caption: "Beautiful sunset at the beach"
```

Response:
```json
{
  "success": true,
  "data": {
    "mediaId": "media-456",
    "url": "https://...?sv=2023-01-03&...",
    "thumbnailUrl": "https://...?sv=2023-01-03&...",
    "type": "photo",
    "width": 1920,
    "height": 1080,
    "uploadedAt": "2024-01-15T10:00:00Z"
  }
}
```

### 2. Get Trip Media Gallery

```bash
GET /api/media/trip/trip-123?skip=0&limit=20
Authorization: Bearer {token}
```

Response:
```json
{
  "success": true,
  "data": [
    {
      "mediaId": "media-456",
      "url": "https://...",
      "thumbnailUrl": "https://...",
      "type": "photo",
      "width": 1920,
      "height": 1080,
      "uploadedAt": "2024-01-15T10:00:00Z"
    }
  ]
}
```

### 3. Update Media Caption

```bash
PUT /api/media/media-456
Authorization: Bearer {token}
Content-Type: application/json

{
  "caption": "Updated caption",
  "tags": ["sunset", "beach", "vacation"]
}
```

### 4. Delete Media

```bash
DELETE /api/media/media-456
Authorization: Bearer {token}
```

## Image Processing

### Thumbnail Generation

- **Max dimensions**: 300x300 pixels
- **Maintains aspect ratio**: No cropping
- **Format**: JPEG with 85% quality
- **Automatic**: Generated on upload

### Supported Formats

**Images**: JPEG, PNG, GIF, BMP, WEBP
**Videos**: MP4, MOV, AVI (no processing, upload only)

## Security

### Access Control

- **JWT Required**: All endpoints require authentication
- **User Ownership**: Users can only delete their own media
- **SAS Tokens**: Blob URLs use time-limited SAS tokens (1 year validity)
- **Private Container**: Blobs not publicly accessible without SAS

### Content Validation

- File type verification via MIME type
- Size limits configurable via IIS/Kestrel settings
- Malicious file detection via ImageSharp validation

## Observability

- **Structured Logs**: All operations logged with correlation IDs
- **Health Checks**: `/health` endpoint
- **Metrics**: `/metrics` endpoint (Prometheus format)
- **Distributed Tracing**: OpenTelemetry integration

## Integration with Other Services

- **Activity Service**: Media linked to posts via `activityId`
- **Trip Service**: Media galleries per trip via `tripId`
- **User Service**: Profile avatars and user-uploaded content

## Performance

### Upload Flow

1. Receive multipart form data
2. Create media record in MongoDB (status: uploading)
3. Upload original to blob storage
4. Generate thumbnail (images only)
5. Upload thumbnail to blob storage
6. Generate SAS URLs
7. Update media record (status: completed)
8. Publish MediaUploaded event

**Average upload time**: 
- Photo (2MB): ~500ms
- Video (50MB): ~3-5 seconds

### Optimization Tips

- Use CDN for blob storage (Azure CDN)
- Enable blob caching headers
- Implement lazy loading on client
- Use thumbnail URLs for lists/grids

## Future Enhancements

- [ ] Video thumbnail extraction
- [ ] Multiple thumbnail sizes (small, medium, large)
- [ ] Image format conversion (WEBP for web)
- [ ] EXIF data extraction (location, camera info)
- [ ] Duplicate detection (perceptual hashing)
- [ ] Facial recognition tagging
- [ ] Content moderation (Azure Content Moderator)
- [ ] Bulk upload support
- [ ] Background job processing for large files
- [ ] Progressive image loading (LQIP)

## Troubleshooting

### Azurite Connection Issues

```bash
# Clear Azurite data
rm -rf c:\azurite\__blobstorage__

# Restart Azurite
azurite-blob --location c:\azurite
```

### ImageSharp Memory Issues

For large images, configure memory limits in appsettings.json:

```json
"ImageProcessing": {
  "MaxInputImageBytes": 50000000
}
```

### SAS Token Expiry

Default validity is 365 days. To regenerate URLs:

```csharp
var newUrl = await _blobStorage.GetSasUrlAsync(media.BlobName, TimeSpan.FromDays(365));
```
