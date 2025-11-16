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
}
