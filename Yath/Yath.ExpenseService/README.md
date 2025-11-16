# Yath Expense Service

Expense tracking, splitting, and settlement service for the Yath travel platform.

## Features

- **Expense Tracking**: Record trip expenses with automatic splitting
- **Balance Management**: Track who owes whom within trip groups
- **Smart Settlement**: Minimize transaction count using greedy algorithm
- **Saga Orchestration**: Use `ExpenseSettlementSaga` for complex settlement workflows
- **Event-Driven**: React to trip events (participant add/remove)

## Technology Stack

- **.NET 8**: Modern C# with minimal APIs
- **MongoDB**: Document storage for expenses, groups, and settlements
- **OmniFlow Framework**: Saga orchestration, message bus, observability
- **JWT Authentication**: Secure API endpoints
- **Serilog + Seq**: Structured logging

## Architecture

### Domain Models

**Expense**: Individual expense records with splits
```csharp
{
    ExpenseId: "guid",
    TripId: "trip-id",
    PaidBy: "user-id",
    Amount: 100.00,
    Currency: "USD",
    Category: "food",
    Splits: [
        { UserId: "user1", Amount: 50, Paid: true },
        { UserId: "user2", Amount: 50, Paid: false }
    ]
}
```

**ExpenseGroup**: Trip-level expense tracking
```csharp
{
    GroupId: "guid",
    TripId: "trip-id",
    Members: ["user1", "user2"],
    TotalExpenses: 100.00,
    Currency: "USD",
    Balances: {
        "user1": 50.00,  // Credit (paid more than their share)
        "user2": -50.00  // Debt (owes money)
    }
}
```

**Settlement**: Payment tracking between users
```csharp
{
    SettlementId: "guid",
    TripId: "trip-id",
    From: "user2",
    To: "user1",
    Amount: 50.00,
    Status: "pending" | "completed" | "cancelled"
}
```

### API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/expenses/trip/{tripId}` | Add expense with splits |
| GET | `/api/expenses/{expenseId}` | Get expense details |
| GET | `/api/expenses/trip/{tripId}` | List trip expenses |
| DELETE | `/api/expenses/{expenseId}` | Delete expense (reverses balances) |
| GET | `/api/expenses/trip/{tripId}/summary` | Get balances summary |
| POST | `/api/expenses/trip/{tripId}/settlements` | Generate optimal settlements |
| POST | `/api/expenses/settlements/{settlementId}/complete` | Mark settlement completed |
| GET | `/api/expenses/trip/{tripId}/settlements` | List trip settlements |

### Event Subscriptions

- **TripCreated**: Initialize expense group for new trips
- **TripParticipantAdded**: Add member to expense group with zero balance
- **TripParticipantRemoved**: Remove member (only if balance is settled)

### Events Published

- **ExpenseAdded**: New expense recorded
- **ExpenseDeleted**: Expense removed
- **SettlementCreated**: Settlement generated
- **SettlementCompleted**: Settlement marked complete

## Settlement Algorithm

Uses **greedy algorithm** to minimize number of transactions:

1. Separate users into debtors (negative balance) and creditors (positive balance)
2. Sort both groups by amount (descending)
3. Match largest debtor with largest creditor
4. Create settlement for minimum of (debt, credit)
5. Adjust balances and repeat

**Example**:
- Alice: +$100 (credit)
- Bob: -$60 (debt)
- Carol: -$40 (debt)

**Result**: 2 settlements
- Bob → Alice: $60
- Carol → Alice: $40

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "yath-api",
    "Audience": "yath-users"
  },
  "Seq": {
    "ServerUrl": "http://localhost:5341"
  },
  "Urls": "http://localhost:5003"
}
```

## Database Schema

### MongoDB Collections

**expenses**
- Index: `expenseId` (unique)
- Index: `tripId`
- Index: `paidBy`
- Index: `date` (descending)

**expense_groups**
- Index: `groupId` (unique)
- Index: `tripId` (unique)

**settlements**
- Index: `settlementId` (unique)
- Index: `tripId`
- Index: `status`

## Running Locally

```bash
# Start MongoDB
docker run -d -p 27017:27017 --name mongo mongo:latest

# Start Seq (optional)
docker run -d -p 5341:80 --name seq datalust/seq:latest

# Run service
cd Yath/Yath.ExpenseService/Yath.ExpenseService
dotnet run
```

Service available at: `http://localhost:5003`

Swagger UI: `http://localhost:5003/swagger`

## Example Usage

### 1. Add Expense

```bash
POST /api/expenses/trip/trip-123
Authorization: Bearer {token}
Content-Type: application/json

{
  "amount": 120.00,
  "currency": "USD",
  "category": "food",
  "description": "Dinner at restaurant",
  "splits": [
    { "userId": "user1", "amount": 40.00 },
    { "userId": "user2", "amount": 40.00 },
    { "userId": "user3", "amount": 40.00 }
  ],
  "date": "2024-01-15T19:30:00Z"
}
```

### 2. Get Balance Summary

```bash
GET /api/expenses/trip/trip-123/summary
Authorization: Bearer {token}
```

Response:
```json
{
  "success": true,
  "data": {
    "tripId": "trip-123",
    "totalExpenses": 120.00,
    "currency": "USD",
    "userTotals": {
      "user1": 80.00,  // Paid $120, owes $40
      "user2": -40.00, // Owes $40
      "user3": -40.00  // Owes $40
    },
    "balances": [
      {
        "fromUserId": "user2",
        "toUserId": "user1",
        "amount": 40.00
      },
      {
        "fromUserId": "user3",
        "toUserId": "user1",
        "amount": 40.00
      }
    ]
  }
}
```

### 3. Generate Settlements

```bash
POST /api/expenses/trip/trip-123/settlements
Authorization: Bearer {token}
```

### 4. Complete Settlement

```bash
POST /api/expenses/settlements/settlement-456/complete
Authorization: Bearer {token}
```

## Observability

- **Structured Logs**: All operations logged with correlation IDs
- **OpenTelemetry**: Distributed tracing for saga operations
- **Health Checks**: `/health` endpoint
- **Metrics**: `/metrics` endpoint (Prometheus format)

## Integration with Other Services

- **Trip Service**: Expense groups created automatically on `TripCreated`
- **Notification Service**: Notifications sent when settlements created
- **User Service**: Enriches expense/settlement DTOs with user details (client-side)

## Future Enhancements

- [ ] Multiple currency support with real-time conversion
- [ ] Receipt OCR using Azure Computer Vision
- [ ] Recurring expenses (subscription splitting)
- [ ] Export to PDF/CSV
- [ ] WhatsApp/Telegram payment reminders
- [ ] Integration with payment gateways (Stripe, PayPal)
- [ ] Budget tracking and alerts
