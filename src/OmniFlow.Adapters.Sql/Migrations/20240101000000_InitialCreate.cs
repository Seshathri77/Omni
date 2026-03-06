using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniFlow.Adapters.Sql.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SagaStates table
            migrationBuilder.CreateTable(
                name: "SagaStates",
                columns: table => new
                {
                    SagaId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SagaType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaStates", x => x.SagaId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SagaStates_CorrelationId",
                table: "SagaStates",
                column: "CorrelationId");

            // IdempotencyRecords table
            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConsumerName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => new { x.MessageId, x.ConsumerName });
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ProcessedAt",
                table: "IdempotencyRecords",
                column: "ProcessedAt");

            // SagaTimers table
            migrationBuilder.CreateTable(
                name: "SagaTimers",
                columns: table => new
                {
                    TimerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SagaId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TimerName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    FireAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    FiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaTimers", x => x.TimerId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SagaTimers_SagaId",
                table: "SagaTimers",
                column: "SagaId");

            migrationBuilder.CreateIndex(
                name: "IX_SagaTimers_IsCompleted_FireAt",
                table: "SagaTimers",
                columns: new[] { "IsCompleted", "FireAt" });

            // DistributedLocks table
            migrationBuilder.CreateTable(
                name: "DistributedLocks",
                columns: table => new
                {
                    LockKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributedLocks", x => x.LockKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributedLocks_ExpiresAt",
                table: "DistributedLocks",
                column: "ExpiresAt");

            // DeadLetterQueue table
            migrationBuilder.CreateTable(
                name: "DeadLetterQueue",
                columns: table => new
                {
                    DeadLetterMessageId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MessageBody = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    OriginalQueue = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    OriginalMessageType = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FailureReasons = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FirstFailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastFailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeadLetterQueue", x => x.DeadLetterMessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterQueue_CorrelationId",
                table: "DeadLetterQueue",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterQueue_RetryCount_NextRetryAt",
                table: "DeadLetterQueue",
                columns: new[] { "RetryCount", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SagaStates");
            migrationBuilder.DropTable(name: "IdempotencyRecords");
            migrationBuilder.DropTable(name: "SagaTimers");
            migrationBuilder.DropTable(name: "DistributedLocks");
            migrationBuilder.DropTable(name: "DeadLetterQueue");
        }
    }
}
