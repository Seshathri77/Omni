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

    private record TestMessage(string Value);
}
