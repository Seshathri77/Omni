using FluentAssertions;
using OmniFlow.Core;
using Xunit;

namespace OmniFlow.Tests.Core;

public class MessageEnvelopeTests
{
    [Fact]
    public void Should_Create_Envelope_With_Message()
    {
        // Arrange
        var message = new TestMessage("Test");
        var accessor = new CorrelationAccessor();
        accessor.SetContext("correlation-123");

        // Act
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);

        // Assert
        envelope.Message.Should().Be(message);
        envelope.CorrelationId.Should().Be("correlation-123");
        envelope.MessageId.Should().NotBeNullOrEmpty();
        envelope.MessageType.Should().Be(nameof(TestMessage));
    }

    [Fact]
    public void Should_Set_Timestamp()
    {
        // Arrange
        var message = new TestMessage("Test");
        var accessor = new CorrelationAccessor();

        // Act
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);

        // Assert
        envelope.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MessageContext_FromEnvelope_Without_CancellationToken_Should_Use_None()
    {
        // Arrange
        var message = new TestMessage("Test");
        var accessor = new CorrelationAccessor();
        accessor.SetContext("correlation-123");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);

        // Act
        var context = MessageContext.FromEnvelope(envelope);

        // Assert
        context.CorrelationId.Should().Be("correlation-123");
        context.MessageId.Should().Be(envelope.MessageId);
        context.MessageType.Should().Be(nameof(TestMessage));
        context.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public void MessageContext_FromEnvelope_With_CancellationToken_Should_Include_Token()
    {
        // Arrange
        var message = new TestMessage("Test");
        var accessor = new CorrelationAccessor();
        accessor.SetContext("correlation-123");
        var envelope = MessageEnvelope<TestMessage>.Create(message, accessor);
        var cts = new CancellationTokenSource();

        // Act
        var context = MessageContext.FromEnvelope(envelope, cts.Token);

        // Assert
        context.CorrelationId.Should().Be("correlation-123");
        context.CancellationToken.Should().Be(cts.Token);
        context.CancellationToken.CanBeCanceled.Should().BeTrue();
    }

    private record TestMessage(string Value);
}
