# Quick Start: Firebase Setup (5 Minutes)

## 🚀 Fast Track Setup

### 1. Get Firebase Credentials (2 min)

1. Go to: https://console.firebase.google.com/
2. Click **"Create a project"** → Name it **"Yath"**
3. After project created, click **⚙️ Settings** (top left) → **"Project settings"**
4. Click **"Service accounts"** tab
5. Click **"Generate new private key"** → **"Generate key"**
6. Save downloaded JSON file

### 2. Install Credentials (1 min)

**Copy the file:**
```powershell
# Navigate to Notification Service directory
cd Yath\Yath.NotificationService\Yath.NotificationService

# Copy your downloaded file here and rename it
copy "C:\Users\YourName\Downloads\yath-xxxxx-firebase-adminsdk.json" firebase-adminsdk.json
```

**Verify it's there:**
```powershell
ls firebase-adminsdk.json
```

### 3. Run the Service (30 sec)

**Option A: Local Development**
```powershell
dotnet run
```

**Option B: Docker**
```powershell
cd ..\..\  # Back to Yath root
docker-compose up notification-service
```

### 4. Verify (30 sec)

Open browser: http://localhost:5007/swagger

You should see "Notification Service" Swagger UI with green checkmarks.

### 5. Test (1 min)

1. Get JWT token from User Service (port 5000):
   ```bash
   curl -X POST http://localhost:5000/api/users/login \
     -H "Content-Type: application/json" \
     -d '{"emailOrUsername":"testuser","password":"Test123!"}'
   ```

2. Register a test device:
   ```bash
   curl -X POST http://localhost:5007/api/notifications/devices \
     -H "Authorization: Bearer {your-token}" \
     -H "Content-Type: application/json" \
     -d '{"token":"test-device-token","deviceName":"Test Phone","platform":"android"}'
   ```

## ✅ Done!

Firebase is now configured. Notifications will be automatically sent when:
- User joins a trip
- New message in chat
- Expense added/settled
- Activity post liked/commented

---

## 🔧 Troubleshooting

**"File not found" error?**
```powershell
# Make sure you're in the right directory
cd Yath\Yath.NotificationService\Yath.NotificationService
ls firebase-adminsdk.json  # Should show the file
```

**"Invalid credentials" error?**
- Re-download the JSON from Firebase Console
- Make sure it's named exactly: `firebase-adminsdk.json`
- Check JSON is valid: https://jsonlint.com/

**Need detailed setup?**
See [FCM_SETUP_GUIDE.md](FCM_SETUP_GUIDE.md) for complete instructions.
