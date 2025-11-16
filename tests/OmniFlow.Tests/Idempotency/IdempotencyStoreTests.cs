using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using OmniFlow.Idempotency;
using Xunit;

namespace OmniFlow.Tests.Idempotency;

public class IdempotencyStoreTests
{
    [Fact]
    public async Task Should_Record_Message_First_Time()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);
        var messageId = Guid.NewGuid().ToString();

        // Act
        var result = await store.TryRecordAsync(messageId, "TestConsumer");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Not_Record_Message_Twice()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);
        var messageId = Guid.NewGuid().ToString();

        // Act
        await store.TryRecordAsync(messageId, "TestConsumer");
        var result = await store.TryRecordAsync(messageId, "TestConsumer");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Check_If_Message_Exists()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);
        var messageId = Guid.NewGuid().ToString();

        // Act
        await store.TryRecordAsync(messageId, "TestConsumer");
        var exists = await store.ExistsAsync(messageId, "TestConsumer");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Remove_Message_Record()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);
        var messageId = Guid.NewGuid().ToString();

        // Act
        await store.TryRecordAsync(messageId, "TestConsumer");
        await store.RemoveAsync(messageId, "TestConsumer");
        var exists = await store.ExistsAsync(messageId, "TestConsumer");

        // Assert
        exists.Should().BeFalse();
    }
}
