using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Yath.NotificationService.Models;

namespace Yath.NotificationService.Services;

public class FcmService : IFcmService
{
    private readonly ILogger<FcmService> _logger;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    public FcmService(IConfiguration configuration, ILogger<FcmService> logger)
    {
        _logger = logger;
        InitializeFirebase(configuration);
    }

    private void InitializeFirebase(IConfiguration configuration)
    {
        try
        {
            var credentialsPath = configuration["Firebase:CredentialsPath"];
            
            if (string.IsNullOrEmpty(credentialsPath) || !File.Exists(credentialsPath))
            {
                _logger.LogWarning("Firebase credentials file not found at {Path}. Push notifications disabled.", credentialsPath);
                _isInitialized = false;
                return;
            }

            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(credentialsPath)
                });

                _logger.LogInformation("Firebase initialized successfully");
                _isInitialized = true;
            }
            else
            {
                _isInitialized = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Firebase");
            _isInitialized = false;
        }
    }

    public async Task<string?> SendNotificationAsync(DeviceToken deviceToken, Models.Notification notification)
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("FCM not initialized. Cannot send notification.");
            return null;
        }

        try
        {
            var messageData = new Dictionary<string, string>
            {
                { "notificationId", notification.NotificationId },
                { "type", notification.Type.ToString() },
                { "priority", notification.Priority.ToString() },
                { "actionUrl", notification.ActionUrl ?? string.Empty },
                { "relatedEntityId", notification.RelatedEntityId ?? string.Empty },
                { "relatedEntityType", notification.RelatedEntityType ?? string.Empty }
            };

            // Add custom payload data
            foreach (var kvp in notification.Payload)
            {
                if (!messageData.ContainsKey(kvp.Key))
                {
                    messageData[kvp.Key] = kvp.Value;
                }
            }

            var message = new Message
            {
                Token = deviceToken.Token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = notification.Title,
                    Body = notification.Body,
                    ImageUrl = notification.ImageUrl
                },
                Data = messageData,
                Android = new AndroidConfig
                {
                    Priority = notification.Priority >= Models.NotificationPriority.High 
                        ? Priority.High 
                        : Priority.Normal,
                    Notification = new AndroidNotification
                    {
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK",
                        Sound = "default",
                        ChannelId = GetChannelId(notification.Type)
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Alert = new ApsAlert
                        {
                            Title = notification.Title,
                            Body = notification.Body
                        },
                        Sound = "default",
                        Badge = 1,
                        MutableContent = true
                    }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("Successfully sent notification to {Token}: {Response}", 
                deviceToken.Token.Substring(0, Math.Min(20, deviceToken.Token.Length)), response);
            
            return response;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "Failed to send FCM notification to {Token}. Error: {ErrorCode}", 
                deviceToken.Token.Substring(0, Math.Min(20, deviceToken.Token.Length)), ex.MessagingErrorCode);
            
            // Handle invalid tokens
            if (ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument || 
                ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                _logger.LogWarning("Token {Token} is invalid or unregistered", 
                    deviceToken.Token.Substring(0, Math.Min(20, deviceToken.Token.Length)));
            }
            
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending FCM notification");
            throw;
        }
    }

    public async Task<Dictionary<string, string?>> SendToMultipleDevicesAsync(
        List<DeviceToken> deviceTokens, 
        Models.Notification notification)
    {
        var results = new Dictionary<string, string?>();

        foreach (var deviceToken in deviceTokens)
        {
            try
            {
                var messageId = await SendNotificationAsync(deviceToken, notification);
                results[deviceToken.Token] = messageId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send to device {Token}", 
                    deviceToken.Token.Substring(0, Math.Min(20, deviceToken.Token.Length)));
                results[deviceToken.Token] = null;
            }
        }

        return results;
    }

    public async Task<string?> SendDataMessageAsync(DeviceToken deviceToken, Dictionary<string, string> data)
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("FCM not initialized. Cannot send data message.");
            return null;
        }

        try
        {
            var message = new Message
            {
                Token = deviceToken.Token,
                Data = data
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("Successfully sent data message to {Token}", 
                deviceToken.Token.Substring(0, Math.Min(20, deviceToken.Token.Length)));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM data message");
            throw;
        }
    }

    private string GetChannelId(NotificationType type)
    {
        return type switch
        {
            NotificationType.TripInvite => "trip_invites",
            NotificationType.TripUpdate => "trip_updates",
            NotificationType.NewMessage => "messages",
            NotificationType.NewComment => "social",
            NotificationType.NewLike => "social",
            NotificationType.NewFollower => "social",
            NotificationType.ExpenseAdded => "expenses",
            NotificationType.ExpenseSettlement => "expenses",
            NotificationType.LocationShared => "location",
            NotificationType.MediaTagged => "media",
            NotificationType.TripReminder => "reminders",
            NotificationType.System => "system",
            _ => "default"
        };
    }
}
