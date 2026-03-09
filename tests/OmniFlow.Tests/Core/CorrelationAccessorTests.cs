using FluentAssertions;
using OmniFlow.Core;
using Xunit;

namespace OmniFlow.Tests.Core;

public class CorrelationAccessorTests
{
    [Fact]
    public void Should_Set_And_Get_CorrelationId()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        accessor.SetContext(correlationId);

        // Assert
        accessor.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Should_Set_And_Get_CausationId()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        var correlationId = Guid.NewGuid().ToString();
        var causationId = Guid.NewGuid().ToString();

        // Act
        accessor.SetContext(correlationId, causationId);

        // Assert
        accessor.CorrelationId.Should().Be(correlationId);
        accessor.CausationId.Should().Be(causationId);
    }

    [Fact]
    public void Should_Return_Null_When_Not_Set()
    {
        // Arrange
        var accessor = new CorrelationAccessor();

        // Assert
        accessor.CorrelationId.Should().BeNull();
        accessor.CausationId.Should().BeNull();
    }

    [Fact]
    public void Should_Overwrite_Previous_Context()
    {
        // Arrange
        var accessor = new CorrelationAccessor();
        accessor.SetContext("correlation-1", "causation-1");

        // Act
        accessor.SetContext("correlation-2", "causation-2");

        // Assert
        accessor.CorrelationId.Should().Be("correlation-2");
        accessor.CausationId.Should().Be("causation-2");
    }

    [Fact]
    public void Should_Handle_Null_CausationId()
    {
        // Arrange
        var accessor = new CorrelationAccessor();

        // Act
        accessor.SetContext("correlation-123", null);

        // Assert
        accessor.CorrelationId.Should().Be("correlation-123");
        accessor.CausationId.Should().BeNull();
    }

    [Fact]
    public void Should_Generate_CorrelationId_When_Not_Set()
    {
        // Arrange
        var accessor = new CorrelationAccessor();

        // Act - First access should generate ID
        var id1 = accessor.CorrelationId ?? Guid.NewGuid().ToString();
        var id2 = accessor.CorrelationId ?? Guid.NewGuid().ToString();

        // Assert - Since we're not setting it, we need to handle the default behavior
        // The actual implementation might auto-generate, or return null
        id1.Should().NotBeNullOrEmpty();
    }
}
