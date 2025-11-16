namespace Yath.Shared.DTOs;

// ============================================================================
// USER DTOs
// ============================================================================

public record UserDto(
    string UserId,
    string Username,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? Location,
    List<string> TravelStyles,
    int FollowersCount,
    int FollowingCount,
    DateTime CreatedAt
);

public record UserProfileDto(
    string UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? Location,
    List<string> TravelStyles,
    int FollowersCount,
    int FollowingCount,
    bool IsFollowing,
    DateTime CreatedAt
);

public record RegisterUserRequest(
    string Username,
    string Email,
    string Password,
    string DisplayName
);

public record LoginRequest(
    string EmailOrUsername,
    string Password
);

public record LoginResponse(
    string Token,
    UserDto User
);

public record UpdateProfileRequest(
    string? DisplayName,
    string? Bio,
    string? Location,
    List<string>? TravelStyles
);

// ============================================================================
// ACTIVITY DTOs
// ============================================================================

public record ActivityDto(
    string ActivityId,
    string UserId,
    string Username,
    string UserDisplayName,
    string? UserAvatarUrl,
    string? TripId,
    string? TripName,
    string Caption,
    LocationInfoDto? Location,
    List<string> Tags,
    List<MediaDto> Media,
    int LikesCount,
    int CommentsCount,
    bool IsLiked,
    string Visibility,
    DateTime CreatedAt
);

public record CreateActivityRequest(
    string? TripId,
    string Caption,
    LocationInfoDto? Location,
    List<string> Tags,
    List<string> MediaIds,
    string Visibility = "public"
);

public record CommentDto(
    string CommentId,
    string ActivityId,
    string UserId,
    string Username,
    string UserDisplayName,
    string? UserAvatarUrl,
    string Text,
    DateTime CreatedAt
);

public record AddCommentRequest(
    string Text
);

public record LocationInfoDto(
    string Name,
    double Latitude,
    double Longitude,
    string? PlaceId
);

// ============================================================================
// TRIP DTOs
// ============================================================================

public record TripDto(
    string TripId,
    string CreatorId,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    List<string> Destinations,
    List<TripParticipantDto> Participants,
    string Status,
    string Visibility,
    string? CoverImageUrl,
    DateTime CreatedAt
);

public record TripParticipantDto(
    string UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    DateTime JoinedAt
);

public record CreateTripRequest(
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    List<string> Destinations,
    string Visibility = "private"
);

public record UpdateTripRequest(
    string? Title,
    string? Description,
    DateTime? StartDate,
    DateTime? EndDate
);

public record ItineraryDayDto(
    int Day,
    DateTime Date,
    List<ItineraryActivityDto> Activities
);

public record ItineraryActivityDto(
    string Time,
    string Title,
    LocationInfoDto Location,
    string Type,
    string? Notes,
    string? BookingInfo
);

public record AddItineraryRequest(
    int Day,
    DateTime Date,
    List<ItineraryActivityDto> Activities
);

// ============================================================================
// EXPENSE DTOs
// ============================================================================

public record ExpenseDto(
    string ExpenseId,
    string TripId,
    string PaidBy,
    string PaidByName,
    decimal Amount,
    string Currency,
    string Category,
    string Description,
    List<ExpenseSplitDto> Splits,
    string? ReceiptUrl,
    DateTime Date,
    DateTime CreatedAt
);

public record ExpenseSplitDto(
    string UserId,
    string Username,
    string DisplayName,
    decimal Amount,
    bool IsPaid
);

public record AddExpenseRequest(
    decimal Amount,
    string Currency,
    string Category,
    string Description,
    List<ExpenseSplitRequest> Splits,
    string? ReceiptUrl,
    DateTime Date
);

public record ExpenseSplitRequest(
    string UserId,
    decimal Amount
);

public record ExpenseSummaryDto(
    string TripId,
    decimal TotalExpenses,
    string Currency,
    Dictionary<string, decimal> UserTotals,
    List<BalanceDto> Balances
);

public record BalanceDto(
    string FromUserId,
    string FromUserName,
    string ToUserId,
    string ToUserName,
    decimal Amount
);

public record RecordSettlementRequest(
    string ToUserId,
    decimal Amount,
    string Currency
);

public record SettlementDto(
    string SettlementId,
    string TripId,
    string FromUserId,
    string FromUserName,
    string ToUserId,
    string ToUserName,
    decimal Amount,
    string Currency,
    string Status,
    DateTime? SettledAt,
    DateTime CreatedAt
);

// ============================================================================
// MEDIA DTOs
// ============================================================================

public record MediaDto(
    string MediaId,
    string Url,
    string? ThumbnailUrl,
    string Type, // "photo", "video"
    int Width,
    int Height,
    DateTime UploadedAt
);

public record UploadMediaResponse(
    string MediaId,
    string Url,
    string Status
);

// ============================================================================
// LOCATION DTOs
// ============================================================================

public record LocationUpdateDto(
    string UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    double Latitude,
    double Longitude,
    double Accuracy,
    DateTime Timestamp
);

public record LocationHistoryDto(
    string UserId,
    List<LocationPointDto> Points
);

public record LocationPointDto(
    double Latitude,
    double Longitude,
    DateTime Timestamp
);

// ============================================================================
// CHAT DTOs
// ============================================================================

public record ChatRoomDto(
    string RoomId,
    string TripId,
    string TripName,
    List<string> ParticipantIds,
    int UnreadCount,
    MessageDto? LastMessage,
    DateTime CreatedAt
);

public record MessageDto(
    string MessageId,
    string RoomId,
    string UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string? Text,
    string? MediaUrl,
    LocationInfoDto? Location,
    List<string> ReadBy,
    DateTime Timestamp
);

public record SendMessageRequest(
    string? Text,
    string? MediaUrl,
    LocationInfoDto? Location
);

// ============================================================================
// NOTIFICATION DTOs
// ============================================================================

public record NotificationDto(
    string NotificationId,
    string UserId,
    string Type,
    string Title,
    string Body,
    Dictionary<string, string> Payload,
    bool IsRead,
    DateTime CreatedAt
);

public record RegisterDeviceTokenRequest(
    string DeviceToken,
    string Platform
);

// ============================================================================
// COMMON DTOs
// ============================================================================

public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Error = null
);

public record PagedResponse<T>(
    List<T> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore
);
