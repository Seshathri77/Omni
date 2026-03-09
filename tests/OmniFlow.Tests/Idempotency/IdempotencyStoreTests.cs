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

    [Fact]
    public async Task Should_Not_Exist_For_Non_Recorded_Message()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);

        // Act
        var exists = await store.ExistsAsync("non-existent", "TestConsumer");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Handle_Different_Consumers_Independently()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);
        var messageId = Guid.NewGuid().ToString();

        // Act
        await store.TryRecordAsync(messageId, "Consumer1");
        var result = await store.TryRecordAsync(messageId, "Consumer2");

        // Assert
        result.Should().BeTrue(); // Different consumer can process same message
    }

    [Fact]
    public async Task Should_Handle_CancellationToken()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);
        var messageId = Guid.NewGuid().ToString();
        var cts = new CancellationTokenSource();

        // Act
        var result = await store.TryRecordAsync(messageId, "TestConsumer", null, cts.Token);
        var exists = await store.ExistsAsync(messageId, "TestConsumer", cts.Token);
        await store.RemoveAsync(messageId, "TestConsumer", cts.Token);

        // Assert
        result.Should().BeTrue();
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Remove_Should_Not_Throw_For_Non_Existent_Message()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(cache);

        // Act
        var act = async () => await store.RemoveAsync("non-existent", "TestConsumer");

        // Assert
        await act.Should().NotThrowAsync();
    }
}
