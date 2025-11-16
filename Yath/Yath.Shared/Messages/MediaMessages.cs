using OmniFlow.Core;

namespace Yath.Shared.Messages;

// ============================================================================
// MEDIA COMMANDS
// ============================================================================

public record UploadMedia(
    string UploadedBy,
    string FileName,
    string MimeType,
    long FileSize,
    byte[] FileData
) : ICommand;

public record ProcessMedia(
    string MediaId
) : ICommand;

public record DeleteMedia(
    string MediaId,
    string UserId
) : ICommand;

// ============================================================================
// MEDIA EVENTS
// ============================================================================

public record MediaUploaded(
    string MediaId,
    string UploadedBy,
    string OriginalUrl,
    string FileName,
    string MimeType,
    long FileSize,
    DateTime UploadedAt
) : IEvent;

public record MediaProcessingStarted(
    string MediaId,
    DateTime StartedAt
) : IEvent;

public record MediaProcessingCompleted(
    string MediaId,
    string OptimizedUrl,
    Dictionary<string, string> Thumbnails, // small, medium, large
    int Width,
    int Height,
    DateTime CompletedAt
) : IEvent;

public record MediaProcessingFailed(
    string MediaId,
    string Error,
    DateTime FailedAt
) : IEvent;

public record MediaDeleted(
    string MediaId,
    DateTime DeletedAt
) : IEvent;
