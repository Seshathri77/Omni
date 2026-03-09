using FluentAssertions;
using OmniFlow.Core;
using OmniFlow.Messaging;
using Xunit;

namespace OmniFlow.Tests.Messaging;

public class MessageBusOptionsTests
{
    [Fact]
    public void Should_Have_Default_Values()
    {
        // Arrange & Act
        var options = new MessageBusOptions();

        // Assert
        options.Provider.Should().Be(MessageBusProvider.InMemory);
        options.ServiceName.Should().Be("default");
        options.UseCorrelation.Should().BeTrue();
        options.UseLogging.Should().BeTrue();
        options.UseRetry.Should().BeTrue();
        options.MaxRetries.Should().Be(3);
        options.EnableCircuitBreaker.Should().BeTrue();
        options.CircuitBreakerFailureRatio.Should().Be(0.5);
    }

    [Fact]
    public void Should_Allow_Custom_Provider()
    {
        // Arrange & Act
        var options = new MessageBusOptions
        {
            Provider = MessageBusProvider.RabbitMQ
        };

        // Assert
        options.Provider.Should().Be(MessageBusProvider.RabbitMQ);
    }

    [Fact]
    public void Should_Allow_Custom_ServiceName()
    {
        // Arrange & Act
        var options = new MessageBusOptions
        {
            ServiceName = "MyService"
        };

        // Assert
        options.ServiceName.Should().Be("MyService");
    }

    [Fact]
    public void Should_Allow_Disabling_Middleware()
    {
        // Arrange & Act
        var options = new MessageBusOptions
        {
            UseCorrelation = false,
            UseLogging = false,
            UseRetry = false
        };

        // Assert
        options.UseCorrelation.Should().BeFalse();
        options.UseLogging.Should().BeFalse();
        options.UseRetry.Should().BeFalse();
    }

    [Fact]
    public void Should_Allow_Custom_Retry_Settings()
    {
        // Arrange & Act
        var options = new MessageBusOptions
        {
            MaxRetries = 5,
            EnableCircuitBreaker = false
        };

        // Assert
        options.MaxRetries.Should().Be(5);
        options.EnableCircuitBreaker.Should().BeFalse();
    }

    [Fact]
    public void Should_Allow_Custom_Circuit_Breaker_Settings()
    {
        // Arrange & Act
        var options = new MessageBusOptions
        {
            CircuitBreakerFailureRatio = 0.8,
            CircuitBreakerMinimumThroughput = 20,
            CircuitBreakerSamplingDurationSeconds = 60,
            CircuitBreakerBreakDurationSeconds = 90
        };

        // Assert
        options.CircuitBreakerFailureRatio.Should().Be(0.8);
        options.CircuitBreakerMinimumThroughput.Should().Be(20);
        options.CircuitBreakerSamplingDurationSeconds.Should().Be(60);
        options.CircuitBreakerBreakDurationSeconds.Should().Be(90);
    }

    [Fact]
    public void Should_Support_All_Provider_Types()
    {
        // Arrange & Act & Assert
        var inMemory = new MessageBusOptions { Provider = MessageBusProvider.InMemory };
        var rabbitMQ = new MessageBusOptions { Provider = MessageBusProvider.RabbitMQ };
        var kafka = new MessageBusOptions { Provider = MessageBusProvider.Kafka };
        var serviceBus = new MessageBusOptions { Provider = MessageBusProvider.ServiceBus };

        inMemory.Provider.Should().Be(MessageBusProvider.InMemory);
        rabbitMQ.Provider.Should().Be(MessageBusProvider.RabbitMQ);
        kafka.Provider.Should().Be(MessageBusProvider.Kafka);
        serviceBus.Provider.Should().Be(MessageBusProvider.ServiceBus);
    }

    [Fact]
    public void Should_Allow_Setting_Provider_Specific_Configurations()
    {
        // Arrange & Act
        var options = new MessageBusOptions
        {
            RabbitMQ = new RabbitMQConfig { HostName = "rabbitmq.local" },
            ServiceBus = new ServiceBusConfig { ConnectionString = "test-connection" },
            Kafka = new KafkaConfig { BootstrapServers = "kafka:9092" }
        };

        // Assert
        options.RabbitMQ.Should().NotBeNull();
        options.RabbitMQ!.HostName.Should().Be("rabbitmq.local");
        options.ServiceBus.Should().NotBeNull();
        options.ServiceBus!.ConnectionString.Should().Be("test-connection");
        options.Kafka.Should().NotBeNull();
        options.Kafka!.BootstrapServers.Should().Be("kafka:9092");
    }

    [Fact]
    public void Provider_Configurations_Should_Default_To_Null()
    {
        // Arrange & Act
        var options = new MessageBusOptions();

        // Assert  
        options.RabbitMQ.Should().BeNull();
        options.ServiceBus.Should().BeNull();
        options.Kafka.Should().BeNull();
    }
}
