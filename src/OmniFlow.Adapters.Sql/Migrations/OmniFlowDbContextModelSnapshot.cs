using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OmniFlow.Adapters.Sql;

#nullable disable

namespace OmniFlow.Adapters.Sql.Migrations
{
    [DbContext(typeof(OmniFlowDbContext))]
    partial class OmniFlowDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.0");

            modelBuilder.Entity("OmniFlow.Adapters.Sql.SagaStateEntity", b =>
                {
                    b.Property<string>("SagaId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("SagaType")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("StateJson")
                        .IsRequired()
                        .HasMaxLength(8000)
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("Version")
                        .HasColumnType("int");

                    b.HasKey("SagaId");

                    b.HasIndex("CorrelationId");

                    b.ToTable("SagaStates");
                });

            modelBuilder.Entity("OmniFlow.Adapters.Sql.IdempotencyRecord", b =>
                {
                    b.Property<string>("MessageId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ConsumerName")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset?>("ExpiresAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("ProcessedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("MessageId", "ConsumerName");

                    b.HasIndex("ProcessedAt");

                    b.ToTable("IdempotencyRecords");
                });

            modelBuilder.Entity("OmniFlow.Sagas.SagaTimer", b =>
                {
                    b.Property<string>("TimerId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset?>("CancelledAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("FireAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("FiredAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsCompleted")
                        .HasColumnType("bit");

                    b.Property<string>("SagaId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("TimerName")
                        .IsRequired()
                        .HasColumnType("nvarchar(200)");

                    b.HasKey("TimerId");

                    b.HasIndex("SagaId");

                    b.HasIndex("IsCompleted", "FireAt");

                    b.ToTable("SagaTimers");
                });

            modelBuilder.Entity("OmniFlow.Sagas.DistributedLockEntity", b =>
                {
                    b.Property<string>("LockKey")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset>("AcquiredAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Owner")
                        .IsRequired()
                        .HasColumnType("nvarchar(200)");

                    b.HasKey("LockKey");

                    b.HasIndex("ExpiresAt");

                    b.ToTable("DistributedLocks");
                });

            modelBuilder.Entity("OmniFlow.Adapters.Sql.DeadLetterQueueEntity", b =>
                {
                    b.Property<string>("DeadLetterMessageId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("FailureReasons")
                        .IsRequired()
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<DateTimeOffset>("FirstFailedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("LastFailedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("MessageBody")
                        .IsRequired()
                        .HasMaxLength(8000)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MessageId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset?>("NextRetryAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("OriginalMessageType")
                        .IsRequired()
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("OriginalQueue")
                        .IsRequired()
                        .HasColumnType("nvarchar(200)");

                    b.Property<int>("RetryCount")
                        .HasColumnType("int");

                    b.HasKey("DeadLetterMessageId");

                    b.HasIndex("CorrelationId");

                    b.HasIndex("RetryCount", "NextRetryAt");

                    b.ToTable("DeadLetterQueue");
                });
#pragma warning restore 612, 618
        }
    }
}
