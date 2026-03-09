using FluentAssertions;
using OmniFlow.Core;
using OmniFlow.Messaging;
using Xunit;

namespace OmniFlow.Tests.Messaging;

public class DeadLetterQueueOptionsTests
{
    [Fact]
    public void Should_Have_Default_Values()
    {
        // Arrange & Act
        var options = new DeadLetterQueueOptions();

        // Assert
        options.MaxRetries.Should().Be(3);
        options.QueueName.Should().Be("dead-letter-queue");
        options.ExchangeName.Should().Be("dead-letter-exchange");
        options.Enabled.Should().BeTrue();
        options.MessageTtl.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Should_Allow_Custom_MaxRetries()
    {
        // Arrange & Act
        var options = new DeadLetterQueueOptions
        {
            MaxRetries = 10
        };

        // Assert
        options.MaxRetries.Should().Be(10);
    }

    [Fact]
    public void Should_Allow_Custom_QueueName()
    {
        // Arrange & Act
        var options = new DeadLetterQueueOptions
        {
            QueueName = "my-dlq"
        };

        // Assert
        options.QueueName.Should().Be("my-dlq");
    }

    [Fact]
    public void Should_Allow_Custom_ExchangeName()
    {
        // Arrange & Act
        var options = new DeadLetterQueueOptions
        {
            ExchangeName = "my-dlx"
        };

        // Assert
        options.ExchangeName.Should().Be("my-dlx");
    }

    [Fact]
    public void Should_Allow_Disabling()
    {
        // Arrange & Act
        var options = new DeadLetterQueueOptions
        {
            Enabled = false
        };

        // Assert
        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Should_Allow_Custom_MessageTtl()
    {
        // Arrange & Act
        var options = new DeadLetterQueueOptions
        {
            MessageTtl = TimeSpan.FromHours(12)
        };

        // Assert
        options.MessageTtl.Should().Be(TimeSpan.FromHours(12));
    }

    [Fact]
    public void Should_Allow_Null_MessageTtl()
    {
        // Arrange & Act
        var options = new DeadLetterQueueOptions
        {
            MessageTtl = null
        };

        // Assert
        options.MessageTtl.Should().BeNull();
    }
}

public class DeadLetterMessageTests
{
    [Fact]
    public void Should_Create_DeadLetterMessage()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        accessor.SetContext("correlation-123");
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);

        // Act
        var dlqMessage = new DeadLetterMessage<TestMessage>
        {
            OriginalMessage = envelope,
            RetryCount = 3,
            LastException = "InvalidOperationException: Test error",
            ConsumerName = "TestConsumer"
        };

        // Assert
        dlqMessage.OriginalMessage.Should().Be(envelope);
        dlqMessage.RetryCount.Should().Be(3);
        dlqMessage.LastException.Should().Be("InvalidOperationException: Test error");
        dlqMessage.ConsumerName.Should().Be("TestConsumer");
    }

    [Fact]
    public void Should_Store_Original_Message_Details()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        accessor.SetContext("correlation-123", "causation-456");
        var message = new TestMessage("test-value");
        var envelope = new MessageEnvelope<TestMessage>
        {
            Message = message,
            CorrelationId = "correlation-123",
            CausationId = "causation-456"
        };

        // Act
        var dlqMessage = new DeadLetterMessage<TestMessage>
        {
            OriginalMessage = envelope,
            RetryCount = 5,
            LastException = "Exception details"
        };

        // Assert
        dlqMessage.OriginalMessage.Message.Value.Should().Be("test-value");
        dlqMessage.OriginalMessage.CorrelationId.Should().Be("correlation-123");
        dlqMessage.OriginalMessage.CausationId.Should().Be("causation-456");
    }

    [Fact]
    public void Should_Set_SentToDlqAt_Timestamp()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);

        // Act
        var dlqMessage = new DeadLetterMessage<TestMessage>
        {
            OriginalMessage = envelope,
            RetryCount = 3
        };

        // Assert
        dlqMessage.SentToDlqAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_Allow_Null_LastException()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);

        // Act
        var dlqMessage = new DeadLetterMessage<TestMessage>
        {
            OriginalMessage = envelope,
            RetryCount = 3,
            LastException = null
        };

        // Assert
        dlqMessage.LastException.Should().BeNull();
    }

    [Fact]
    public void Should_Allow_Null_ConsumerName()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var message = new TestMessage("test");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);

        // Act
        var dlqMessage = new DeadLetterMessage<TestMessage>
        {
            OriginalMessage = envelope,
            RetryCount = 3,
            ConsumerName = null
        };

        // Assert
        dlqMessage.ConsumerName.Should().BeNull();
    }

    private record TestMessage(string Value);
}
