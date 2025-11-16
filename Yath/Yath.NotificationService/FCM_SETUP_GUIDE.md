# Firebase Cloud Messaging (FCM) Setup Guide

This guide will help you set up Firebase Cloud Messaging for the Yath Notification Service.

---

## 📋 Prerequisites

- Google Account
- Access to Firebase Console
- .NET 8 SDK installed
- Notification Service built and ready

---

## 🚀 Step-by-Step Setup

### 1. Create Firebase Project

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Click **"Add project"** or **"Create a project"**
3. Enter project name: `Yath` (or your preferred name)
4. Enable/disable Google Analytics (optional)
5. Click **"Create project"**

### 2. Add Android App (for mobile push notifications)

1. In Firebase Console, click the **Android icon** (⚙️ Settings)
2. Click **"Add app"** → Select **Android**
3. Register app:
   - **Android package name:** `com.yath.app` (or your package name)
   - **App nickname (optional):** Yath Mobile
   - **Debug signing certificate SHA-1:** (optional for development)
4. Click **"Register app"**
5. Download `google-services.json` (save for mobile app development)
6. Click **"Next"** → **"Continue to console"**

### 3. Add iOS App (optional)

1. Click **"Add app"** → Select **iOS**
2. Register app:
   - **iOS bundle ID:** `com.yath.app`
   - **App nickname (optional):** Yath iOS
3. Download `GoogleService-Info.plist`
4. Follow iOS setup instructions

---

## 🔑 Generate Service Account Key

### Method 1: Firebase Console UI

1. In Firebase Console, click **⚙️ Settings** → **"Project settings"**
2. Go to **"Service accounts"** tab
3. Click **"Generate new private key"**
4. Click **"Generate key"** in confirmation dialog
5. A JSON file will download automatically - this is your **firebase-adminsdk.json**

### Method 2: Google Cloud Console

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your Firebase project
3. Navigate to **"IAM & Admin"** → **"Service Accounts"**
4. Find service account: `firebase-adminsdk-xxxxx@your-project-id.iam.gserviceaccount.com`
5. Click **⋮** (three dots) → **"Manage keys"**
6. Click **"Add key"** → **"Create new key"**
7. Select **JSON** format
8. Click **"Create"** - JSON file downloads

---

## 📁 Configure Notification Service

### 1. Place Firebase Credentials

**Option A: Local Development**

1. Copy the downloaded JSON file
2. Rename it to **`firebase-adminsdk.json`**
3. Place it in: `Yath.NotificationService/Yath.NotificationService/`

```
Yath.NotificationService/
├── Yath.NotificationService/
│   ├── firebase-adminsdk.json          ← Place here
│   ├── firebase-adminsdk.json.example  ← Example template
│   ├── appsettings.json
│   └── Program.cs
```

**Option B: Docker/Production**

Update the volume mount in `docker-compose.yml`:

```yaml
notification-service:
  volumes:
    - ./Yath.NotificationService/Yath.NotificationService/firebase-adminsdk.json:/app/firebase-adminsdk.json:ro
```

### 2. Verify Configuration

Check `appsettings.json`:

```json
{
  "Firebase": {
    "CredentialsPath": "firebase-adminsdk.json"
  }
}
```

For Docker:
```json
{
  "Firebase": {
    "CredentialsPath": "/app/firebase-adminsdk.json"
  }
}
```

---

## 🧪 Test FCM Setup

### 1. Start Notification Service

**Local:**
```powershell
cd Yath.NotificationService\Yath.NotificationService
dotnet run
```

**Docker:**
```powershell
cd Yath
docker-compose up notification-service
```

### 2. Check Logs

Look for successful FCM initialization:
```
[INF] Firebase Cloud Messaging initialized successfully
[INF] Notification Service started
```

### 3. Register Test Device

**Using Swagger UI:** http://localhost:5007/swagger

1. First, register a user and get JWT token from User Service (port 5000)
2. Click **"Authorize"** in Swagger UI, enter: `Bearer {your-token}`
3. Test `POST /api/notifications/devices`:

```json
{
  "token": "test-fcm-device-token-from-mobile-app",
  "deviceName": "Test Device",
  "platform": "android",
  "deviceModel": "Pixel 7",
  "osVersion": "Android 14",
  "appVersion": "1.0.0"
}
```

**Using cURL:**
```bash
curl -X POST http://localhost:5007/api/notifications/devices \
  -H "Authorization: Bearer {your-jwt-token}" \
  -H "Content-Type: application/json" \
  -d '{
    "token": "your-fcm-device-token",
    "deviceName": "Test Device",
    "platform": "android"
  }'
```

### 4. Send Test Notification

**Option A: Trigger via Event**

Create a trip in Trip Service - this will automatically trigger notifications:

```bash
curl -X POST http://localhost:5001/api/trips \
  -H "Authorization: Bearer {your-jwt-token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Test Trip",
    "description": "Testing notifications",
    "startDate": "2025-12-01",
    "endDate": "2025-12-07",
    "destinations": ["Paris", "London"]
  }'
```

**Option B: Direct Notification (for testing)**

Add a test endpoint in `NotificationsController.cs`:

```csharp
[HttpPost("test-send")]
public async Task<ActionResult> TestSendNotification([FromBody] string userId)
{
    await _fcmService.SendNotificationAsync(
        userId,
        "Test Notification",
        "This is a test from Yath!",
        new Dictionary<string, string> { { "test", "true" } }
    );
    return Ok("Notification sent");
}
```

---

## 📱 Mobile App Integration

### Android (React Native / Flutter / Native)

1. Add `google-services.json` to your Android app
2. Install FCM SDK
3. Get device token
4. Register token with Notification Service:

```javascript
// Example: React Native with @react-native-firebase/messaging
import messaging from '@react-native-firebase/messaging';

async function registerDevice() {
  const token = await messaging().getToken();
  
  await fetch('http://localhost:5007/api/notifications/devices', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${jwtToken}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      token: token,
      deviceName: 'My Phone',
      platform: 'android',
      deviceModel: 'Pixel 7',
      osVersion: 'Android 14',
      appVersion: '1.0.0'
    })
  });
}
```

### iOS

1. Add `GoogleService-Info.plist` to your iOS app
2. Configure APNs (Apple Push Notification service)
3. Get FCM token and register with Notification Service

---

## 🔒 Security Best Practices

### 1. Never Commit Credentials

The `firebase-adminsdk.json` file contains sensitive credentials!

**Already configured:**
- ✅ `.gitignore` includes `firebase-adminsdk.json`
- ✅ Example file provided: `firebase-adminsdk.json.example`

### 2. Environment Variables (Production)

Instead of file path, use environment variables:

```bash
# Set Firebase credentials as environment variable
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/firebase-adminsdk.json"
```

Or use Azure Key Vault / AWS Secrets Manager:

```json
{
  "Firebase": {
    "ProjectId": "from-azure-keyvault",
    "PrivateKey": "from-azure-keyvault",
    "ClientEmail": "from-azure-keyvault"
  }
}
```

### 3. Rotate Keys Regularly

In Firebase Console → Service Accounts → Generate new key periodically.

---

## 🔍 Troubleshooting

### Error: "Failed to initialize Firebase"

**Causes:**
- Missing `firebase-adminsdk.json` file
- Invalid JSON format
- Wrong file path in configuration

**Solution:**
1. Verify file exists at configured path
2. Validate JSON syntax: https://jsonlint.com/
3. Check file permissions (Docker: ensure read access)

### Error: "The request was missing an FCM token"

**Cause:** Device token not registered or invalid

**Solution:**
1. Ensure mobile app obtained FCM token
2. Verify token registered via `/api/notifications/devices`
3. Check token hasn't expired (refresh token if needed)

### Error: "Permission denied"

**Cause:** Service account lacks FCM permissions

**Solution:**
1. In Google Cloud Console → IAM
2. Find firebase-adminsdk service account
3. Ensure role: **"Firebase Cloud Messaging Admin"**

### Notifications Not Received on Device

**Checklist:**
- ✅ Device token registered in Notification Service
- ✅ Mobile app has notification permissions enabled
- ✅ FCM credentials valid
- ✅ Device has internet connection
- ✅ App in foreground or background (not force-stopped)

**Debug:**
```powershell
# Check Notification Service logs
docker-compose logs -f notification-service

# Check MongoDB for device tokens
docker exec -it yath-mongodb mongosh
> use yath_notifications
> db.DeviceTokens.find().pretty()
```

---

## 📊 Monitoring

### View Sent Notifications

**Swagger UI:** http://localhost:5007/swagger
- `GET /api/notifications` - Get user's notifications
- `GET /api/notifications/unread/count` - Unread count

**MongoDB:**
```javascript
docker exec -it yath-mongodb mongosh -u admin -p admin123

use yath_notifications
db.Notifications.find().pretty()
db.DeviceTokens.find().pretty()
```

### Firebase Console Analytics

1. Go to Firebase Console → Cloud Messaging
2. View delivery metrics:
   - Sent notifications
   - Open rate
   - Error rate
   - Device statistics

---

## 🎯 Production Checklist

- [ ] Firebase project created
- [ ] Service account key downloaded
- [ ] `firebase-adminsdk.json` in `.gitignore`
- [ ] Credentials stored in secure vault (Azure Key Vault / AWS Secrets Manager)
- [ ] Environment variables configured
- [ ] Mobile apps integrated with FCM
- [ ] Test notifications working
- [ ] Error handling implemented
- [ ] Monitoring configured (Seq logs)
- [ ] Backup of service account key stored securely

---

## 📚 Additional Resources

- [Firebase Cloud Messaging Documentation](https://firebase.google.com/docs/cloud-messaging)
- [Firebase Admin .NET SDK](https://firebase.google.com/docs/admin/setup)
- [React Native Firebase](https://rnfirebase.io/)
- [Flutter Firebase](https://firebase.flutter.dev/)

---

## 🆘 Need Help?

1. Check Firebase Console → Cloud Messaging for errors
2. View Seq logs: http://localhost:5341
3. Verify service account permissions
4. Test with FCM test endpoint in Firebase Console

---

**Important:** The `firebase-adminsdk.json` file is already added to `.gitignore`. Never commit this file to version control!
